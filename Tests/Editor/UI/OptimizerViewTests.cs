using System;
using System.Linq;
using ClarityGameOptimizer.Core;
using ClarityGameOptimizer.Tests.Core;
using ClarityGameOptimizer.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace ClarityGameOptimizer.Tests.UI
{
    /// <summary>The tree is built on a plain root without a panel, so layout never runs; structure and text are what these check.</summary>
    public class OptimizerViewTests
    {
        private VisualElement _root;
        private OptimizerViewModel _model;
        private OptimizerView _view;

        [SetUp]
        public void Build()
        {
            _root = new VisualElement();
            _model = new OptimizerViewModel();
            _view = new OptimizerView(_root, _model);
        }

        [TearDown]
        public void Dispose()
        {
            _view.Dispose();
        }

        [Test]
        public void The_rail_lists_the_seven_areas_with_architecture_active()
        {
            var items = _root.Query<Button>(className: "cgo-rail-item").ToList();

            Assert.That(items.Select(item => item.name), Is.EqualTo(new[]
            {
                "rail-architecture", "rail-performance", "rail-memory", "rail-build-size", "rail-assets", "rail-dependencies", "rail-code-quality",
            }));
            Assert.That(items[0].ClassListContains("cgo-rail-item--active"), Is.True);
            Assert.That(items[0].ClassListContains("cgo-rail-item--planned"), Is.False);
            Assert.That(items[1].ClassListContains("cgo-rail-item--planned"), Is.True);
            Assert.That(items[1].tooltip, Does.EndWith("Planned for v1.0.0."));
            Assert.That(items[1].Q<Label>(className: "cgo-rail-count").ClassListContains("cgo-rail-count--empty"), Is.True, "no count pill before a scan");
        }

        [Test]
        public void Before_a_scan_the_empty_state_shows_and_the_list_hides()
        {
            Assert.That(_root.Q<Label>("empty").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_root.Q<Label>("empty").text, Does.Contain("Press Scan to read the project."));
            Assert.That(_view.List.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_root.Q("scan").enabledSelf, Is.True);
            Assert.That(_root.Q("scope").enabledSelf, Is.False, "one scope only");
            Assert.That(_view.StatusText, Is.EqualTo("Press Scan to read the project."));
        }

        [Test]
        public void A_stored_report_fills_the_cards_the_list_and_the_rail_count()
        {
            _model.Store(ArchitectureFixtures.Report(), TimeSpan.FromSeconds(1));

            var cards = _root.Query(className: "cgo-card").ToList();
            Assert.That(cards.Count, Is.EqualTo(4));
            Assert.That(cards[0].Q<Label>(className: "cgo-card-value").text, Is.EqualTo("10"));
            Assert.That(cards[0].Q<Label>(className: "cgo-card-label").text, Is.EqualTo("Assemblies"));
            Assert.That(cards[2].Q<Label>(className: "cgo-card-value").text, Is.EqualTo("56 %"));
            Assert.That(_view.List.itemsSource.Count, Is.EqualTo(4));
            Assert.That(_view.List.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_root.Q<Label>("empty").style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_root.Q<Button>("rail-architecture").Q<Label>(className: "cgo-rail-count").text, Is.EqualTo("4"));
            Assert.That(_root.Q<Button>("rail-architecture").Q<Label>(className: "cgo-rail-count").ClassListContains("cgo-rail-count--empty"), Is.False);
            Assert.That(_view.StatusText, Does.StartWith("Scanned Project in 1.0 s: 10 nodes, 4 findings"));
        }

        [Test]
        public void Selecting_a_planned_area_disables_scan_and_explains()
        {
            _view.SelectArea(Area.Memory);

            Assert.That(_root.Q<Button>("rail-memory").ClassListContains("cgo-rail-item--active"), Is.True);
            Assert.That(_root.Q<Button>("rail-architecture").ClassListContains("cgo-rail-item--active"), Is.False);
            Assert.That(_root.Q("scan").enabledSelf, Is.False);
            Assert.That(_root.Q("render").enabledSelf, Is.False);
            Assert.That(_root.Q<Label>("empty").text, Does.Contain("Planned for v0.5.0."));
            Assert.That(_view.StatusText, Is.EqualTo("Memory is not here yet: planned for v0.5.0."));
        }

        [Test]
        public void The_list_has_the_five_columns_in_order()
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (Column column in _view.List.columns)
            {
                names.Add(column.name);
            }

            Assert.That(names, Is.EqualTo(new[] { "severity", "subject", "title", "measured", "verdict" }));
            Assert.That(_view.List.fixedItemHeight, Is.EqualTo(OptimizerView.RowHeight));
        }
    }
}
