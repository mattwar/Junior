using Junior;
using Junior.Helpers;
using static Tests.Helpers.TestHelpers;

namespace Tests
{
    [TestClass]
    public class JsonDataReaderTests
    {
        [TestMethod]
        public async Task TestSingleTable()
        {
            await TestDataReaderAsync(
                IdNameDataJson,
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

        public static readonly string IdNameDataJson =
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
                """;

        [TestMethod]
        public async Task TestReadClassRows()
        {
            await TestReadRows(
                IdNameDataJson,
                new ParameterizedRecord(1, "Tom", 3.2),
                new ParameterizedRecord(2, "Mot", 5.4));

            await TestReadRows(
                IdNameDataJson,
                new InitializedRecord { Id = 1, Name = "Tom", Data = 3.2 },
                new InitializedRecord { Id = 2, Name = "Mot", Data = 5.4 });

            await TestReadRows(
                IdNameDataJson,
                new DefaultValueRecord(1, "Tom"),
                new DefaultValueRecord(2, "Mot"));
        }

        public record ParameterizedRecord(long Id, string Name, double Data);

        public record InitializedRecord()
        {
            public long Id { get; init; }
            public string Name { get; init; } = null!;
            public double Data { get; init; }
        }

        public record DefaultValueRecord(long Id, string Name, decimal Discount=0.1m);

        private async Task TestReadRows<TRow>(string json, params TRow[] expectedRows)
        {
            var reader = new JsonDataReader(JsonTokenReader.Create(new StringReader(json)));

            var actualRows = await reader.ReadRowsAsync<TRow>().ToListAsync();

            AssertStructurallyEqual(expectedRows, actualRows);
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
