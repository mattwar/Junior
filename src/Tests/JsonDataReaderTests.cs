using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Junior;
using static Tests.TestHelpers;

namespace Tests
{
    [TestClass]
    public class JsonDataReaderTests
    {
        [TestMethod]
        public async Task TestSingleTable()
        {
            await TestDataReaderAsync(
            """
                {
                    "name": "Test",
                    "columns":
                    [
                        { "name": "Id", "type": "long" },
                        { "name": "Name", "type": "string" }
                        { "name": "Data", "type": "double" }
                    ], 
                    "rows": 
                    [
                        [1, "Tom", 3.2], 
                        [2, "Mot", 5.4]
                    ]
                }
                """,
            new Table("Test",
                new[] {
                    new Column("Id", "long"),
                    new Column("Name", "string"),
                    new Column("Data", "double")},
                new[]
                {
                    new Row(1L, "Tom", 3.2),
                    new Row(2L, "Mot", 5.4)
                }));
        }

        private Task TestDataReaderAsync(string json, Table table)
        {
            return TestDataReaderAsync(json, new TableSet(new[] { table }));
        }

        private async Task TestDataReaderAsync(string json, TableSet expectedTableSet)
        {
            var reader = new JsonDataReader(JsonTokenReader.Create(new StringReader(json)));
            var tables = new List<Table>();

            while (await reader.MoveToNextTableAsync())
            {
                var name = reader.TableName;
                var rows = new List<Row>();
                var columns = new List<Column>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columns.Add(new Column(reader.GetFieldName(i), reader.GetFieldType(i)));
                }

                while (await reader.MoveToNextRowAsync())
                {
                    var values = new List<object?>();
                    
                    while (await reader.MoveToNextFieldAsync())
                    {
                        var value = await reader.ReadFieldValueAsync();
                        values.Add(value);
                    }

                    rows.Add(new Row(values));
                }

                tables.Add(new Table(name, columns, rows));
            }

            var actualTableSet = new TableSet(tables);

            AssertStructurallyEqual(expectedTableSet, actualTableSet);
        }

        private record Row(IReadOnlyList<object?> Values)
        {
            public Row(params object?[] values)
                : this((IReadOnlyList<object?>)values)
            {
            }
        }

        private record Column(string Name, string Type)
        {
            public Column(string name)
                : this(name, "")
            {
            }
        }

        private record Table(
            string Name, 
            IReadOnlyList<Column> Columns,
            IReadOnlyList<Row> Rows)
        {
        }

        private record TableSet(IReadOnlyList<Table> Tables)
        {
            public TableSet(params Table[] tables)
                : this((IReadOnlyList<Table>)tables)
            {
            }
        }
    }
}
