using System.Collections.Generic;
using System.Linq;
using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class CSharpSourceScannerTests
    {
        private static CodePattern[] Patterns(string source)
        {
            return CSharpSourceScanner.Scan(source).Select(hit => hit.Pattern).ToArray();
        }

        [Test]
        public void Masking_blanks_comments_and_literals_and_keeps_offsets_and_lines()
        {
            const string source = "int a = 1; // GetComponent<X>()\nstring s = \"Camera.main\"; /* Find(\"x\")\n*/ char c = '}';";
            var literals = new List<CSharpSourceScanner.LiteralSpan>();
            string masked = CSharpSourceScanner.Mask(source, literals);

            Assert.That(masked.Length, Is.EqualTo(source.Length));
            Assert.That(masked.Count(ch => ch == '\n'), Is.EqualTo(2));
            Assert.That(masked, Does.Not.Contain("GetComponent").And.Not.Contain("Camera.main").And.Not.Contain("Find"));
            Assert.That(masked, Does.Contain("int a = 1;").And.Contain("string s = \""));
            Assert.That(literals.Count, Is.EqualTo(2));
            Assert.That(literals[1].Character, Is.True);
        }

        [Test]
        public void Verbatim_and_interpolated_strings_are_masked_whole()
        {
            const string source = "var a = @\"C:\\\"\"quoted\"\" Update()\"; var b = $\"{x} {{GetComponent}} {(y ? \"q\" : \"r\")}\";";
            var literals = new List<CSharpSourceScanner.LiteralSpan>();
            string masked = CSharpSourceScanner.Mask(source, literals);

            Assert.That(masked, Does.Not.Contain("Update").And.Not.Contain("GetComponent"));
            Assert.That(literals.Count, Is.EqualTo(2));
            Assert.That(literals[1].Interpolated, Is.True);
            Assert.That(masked.Substring(literals[1].End), Is.EqualTo(";"));
        }

        [Test]
        public void Finds_costly_calls_inside_per_frame_messages_with_their_lines()
        {
            const string source = @"using UnityEngine;
public class Enemy : MonoBehaviour
{
    void Start() { var body = GetComponent<Rigidbody>(); }

    private void Update()
    {
        var body = GetComponent<Rigidbody>();
        var player = GameObject.Find(""Player"");
        if (other.tag == ""Enemy"") { }
        Debug.Log(""tick"");
    }
}";
            List<CodeHit> hits = CSharpSourceScanner.Scan(source);

            Assert.That(hits.Select(h => h.Pattern), Is.EquivalentTo(new[] { CodePattern.GetComponent, CodePattern.Find, CodePattern.TagCompare, CodePattern.Log }));
            Assert.That(hits.Single(h => h.Pattern == CodePattern.GetComponent).Line, Is.EqualTo(8));
            Assert.That(hits.All(h => h.Method == "Update" && h.MethodLine == 6), Is.True);
        }

        [Test]
        public void Patterns_in_comments_strings_and_other_methods_do_not_count()
        {
            const string source = @"void Update()
{
    // GetComponent<X>() each frame would be slow
    label = ""GameObject.Find"";
}
void Refresh() { Destroy(gameObject); }";
            Assert.That(Patterns(source), Is.Empty);
        }

        [Test]
        public void An_empty_per_frame_message_is_reported_and_an_empty_start_is_not()
        {
            const string source = "class A {\n  void Update() { }\n  void LateUpdate()\n  {\n    // nothing yet\n  }\n  void Start() { }\n}";
            List<CodeHit> hits = CSharpSourceScanner.Scan(source);

            Assert.That(hits.Select(h => h.Method), Is.EqualTo(new[] { "Update", "LateUpdate" }));
            Assert.That(hits.All(h => h.Pattern == CodePattern.EmptyMessage), Is.True);
            Assert.That(hits.Select(h => h.Line), Is.EqualTo(new[] { 2, 3 }));
        }

        [Test]
        public void Linq_counts_only_where_the_file_uses_system_linq()
        {
            const string body = "void Update() { var n = enemies.Where(e => e.Alive).ToList(); var c = list.Count; }";
            Assert.That(Patterns(body), Has.No.Member(CodePattern.Linq));
            Assert.That(Patterns("using System.Linq;\n" + body).Count(p => p == CodePattern.Linq), Is.EqualTo(2));
        }

        [Test]
        public void String_building_allocations_are_found()
        {
            const string source = "void LateUpdate() { label.text = $\"{m:00}:{s:00}\"; other.text = \"HP \" + hp; third.text = hp.ToString(); }";
            Assert.That(Patterns(source).Count(p => p == CodePattern.StringAllocation), Is.EqualTo(3));
        }

        [Test]
        public void Allocations_instantiation_and_allocating_queries_are_found()
        {
            const string source = @"void FixedUpdate()
{
    var list = new List<Enemy>();
    var buffer = new Collider[8];
    var points = new[] { a, b };
    Instantiate(prefab);
    var hits = Physics.OverlapSphere(transform.position, 3f);
    var all = Physics.RaycastAll(ray);
    SendMessage(""Hit"");
    var data = Resources.Load<Data>(""data"");
}";
            CodePattern[] patterns = Patterns(source);
            Assert.That(patterns.Count(p => p == CodePattern.CollectionAllocation), Is.EqualTo(3));
            Assert.That(patterns, Contains.Item(CodePattern.InstantiateOrDestroy));
            Assert.That(patterns.Count(p => p == CodePattern.AllocatingPhysicsQuery), Is.EqualTo(2));
            Assert.That(patterns, Contains.Item(CodePattern.SendMessage));
            Assert.That(patterns, Contains.Item(CodePattern.ResourcesLoad));
        }

        [Test]
        public void Overlap_queries_without_a_layer_mask_are_found_anywhere()
        {
            const string source = @"void Explode()
{
    var a = Physics.OverlapSphere(position, radius);
    var b = Physics.OverlapSphere(position, radius, 1 << enemyLayer);
    int c = Physics.OverlapSphereNonAlloc(position, radius, buffer);
    int d = Physics.OverlapSphereNonAlloc(position, radius, buffer, mask);
    var e = Physics.OverlapBox(center, half, Quaternion.identity);
}";
            List<CodeHit> hits = CSharpSourceScanner.Scan(source).Where(h => h.Pattern == CodePattern.PhysicsQueryWithoutMask).ToList();
            Assert.That(hits.Select(h => h.Line), Is.EqualTo(new[] { 3, 5, 7 }));
            Assert.That(hits.All(h => h.Method.Length == 0), Is.True);
        }

        [Test]
        public void Counting_arguments_ignores_nested_commas()
        {
            string masked = CSharpSourceScanner.Mask("Physics.OverlapSphere(Pick(a, b), new Vector3(1, 2, 3), LayerMask.GetMask(\"A\", \"B\"))");
            Assert.That(CSharpSourceScanner.CountArguments(masked, masked.IndexOf('(')), Is.EqualTo(3));
            string empty = CSharpSourceScanner.Mask("Call( )");
            Assert.That(CSharpSourceScanner.CountArguments(empty, empty.IndexOf('(')), Is.EqualTo(0));
        }
    }
}
