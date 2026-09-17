using System;
using System.Globalization;
using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class JsonWriterTests
    {
        [Test]
        public void Writes_empty_containers_on_one_line()
        {
            Assert.That(new JsonWriter().BeginObject().EndObject().ToString(), Is.EqualTo("{}"));
            Assert.That(new JsonWriter().BeginArray().EndArray().ToString(), Is.EqualTo("[]"));
        }

        [Test]
        public void Indents_nested_containers_by_two_spaces_with_lf_line_breaks()
        {
            string json = new JsonWriter()
                .BeginObject()
                .Name("a").Value(1)
                .Name("b").BeginArray().Value(true).Null().EndArray()
                .Name("c").BeginObject().EndObject()
                .EndObject()
                .ToString();

            Assert.That(json, Is.EqualTo("{\n  \"a\": 1,\n  \"b\": [\n    true,\n    null\n  ],\n  \"c\": {}\n}"));
        }

        [Test]
        public void Compact_mode_writes_no_whitespace()
        {
            string json = new JsonWriter(indented: false)
                .BeginObject().Name("a").BeginArray().Value(1).Value("x").EndArray().EndObject()
                .ToString();

            Assert.That(json, Is.EqualTo("{\"a\":[1,\"x\"]}"));
        }

        [Test]
        public void Escapes_quotes_backslashes_and_control_characters()
        {
            string json = new JsonWriter().Value("a\"b\\c\nd\tef/g").ToString();

            Assert.That(json, Is.EqualTo("\"a\\\"b\\\\c\\nd\\te\\u0001f/g\""));
        }

        [Test]
        public void Writes_numbers_with_the_invariant_culture()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                string json = new JsonWriter(indented: false)
                    .BeginArray().Value(1.5).Value(1234567.25).Value(-3L).Value(1e21).EndArray()
                    .ToString();

                Assert.That(json, Is.EqualTo("[1.5,1234567.25,-3,1E+21]"));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void Writes_whole_doubles_without_a_fraction()
        {
            Assert.That(new JsonWriter().Value(62d).ToString(), Is.EqualTo("62"));
        }

        [Test]
        public void Writes_nan_and_infinity_as_null()
        {
            string json = new JsonWriter(indented: false)
                .BeginArray().Value(double.NaN).Value(double.PositiveInfinity).Value(double.NegativeInfinity).EndArray()
                .ToString();

            Assert.That(json, Is.EqualTo("[null,null,null]"));
        }

        [Test]
        public void Writes_a_null_string_as_null()
        {
            Assert.That(new JsonWriter().Value((string)null).ToString(), Is.EqualTo("null"));
        }

        [Test]
        public void Requires_a_name_before_a_value_inside_an_object()
        {
            var json = new JsonWriter().BeginObject();

            Assert.That(() => json.Value(1), Throws.InvalidOperationException);
        }

        [Test]
        public void Rejects_a_name_inside_an_array_and_at_the_root()
        {
            Assert.That(() => new JsonWriter().Name("a"), Throws.InvalidOperationException);
            Assert.That(() => new JsonWriter().BeginArray().Name("a"), Throws.InvalidOperationException);
        }

        [Test]
        public void Rejects_two_names_in_a_row()
        {
            var json = new JsonWriter().BeginObject().Name("a");

            Assert.That(() => json.Name("b"), Throws.InvalidOperationException);
        }

        [Test]
        public void Rejects_closing_an_object_while_a_name_waits_for_its_value()
        {
            var json = new JsonWriter().BeginObject().Name("a");

            Assert.That(() => json.EndObject(), Throws.InvalidOperationException);
        }

        [Test]
        public void Rejects_closing_the_wrong_container()
        {
            Assert.That(() => new JsonWriter().BeginObject().EndArray(), Throws.InvalidOperationException);
            Assert.That(() => new JsonWriter().EndObject(), Throws.InvalidOperationException);
        }

        [Test]
        public void Rejects_a_second_root_value()
        {
            var json = new JsonWriter().Value(1);

            Assert.That(() => json.Value(2), Throws.InvalidOperationException);
        }

        [Test]
        public void ToString_throws_while_a_container_is_open()
        {
            var json = new JsonWriter().BeginObject();

            Assert.That(() => json.ToString(), Throws.InvalidOperationException);
        }
    }
}
