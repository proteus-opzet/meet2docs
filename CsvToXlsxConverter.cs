using System.Data;
using System.Globalization;
using ClosedXML.Excel;

namespace Meet2Docs;

public class CsvToXlsxConverter
{
    public static void Run(string csvPath, string xlsxPath)
    {
        var dataTable = ReadCsvToDataTable(csvPath);
        CreateXlsxWithConditionalFormatting(dataTable, xlsxPath);
        
        Console.WriteLine("Conversion complete.");
    }

    // Step 1: Read CSV to DataTable
    private static DataTable ReadCsvToDataTable(string filePath)
    {
        var dt = new DataTable();
        using var reader = new StreamReader(filePath);
        var headers = reader.ReadLine().Split(',');
        foreach (var header in headers)
            dt.Columns.Add(header);

        while (!reader.EndOfStream)
        {
            var rows = reader.ReadLine().Split(',');
            dt.Rows.Add(rows);
        }

        return dt;
    }

    // Step 2: Write to XLSX and apply conditional formatting
    private static void CreateXlsxWithConditionalFormatting(DataTable dt, string outputPath)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sheet1");
        worksheet.Cell(1, 1).InsertTable(dt);
        var rowCount = dt.Rows.Count;
        var colCount = dt.Columns.Count;

        for (var col = 1; col <= colCount; col++)
        {
            var dataRange = worksheet.Range(2, col, rowCount + 1, col);
            var allNumeric = true;
            var allDateTime = true;

            foreach (var cell in dataRange.Cells())
            {
                var value = cell.Value.ToString();
                if (!double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                    allNumeric = false;
                if (!DateTime.TryParse(value, out _))
                    allDateTime = false;
            }

            if (allNumeric)
            {
                // Format numbers (optional: apply number format)
                foreach (var cell in dataRange.Cells())
                    cell.Value = double.Parse(cell.Value.ToString(), CultureInfo.InvariantCulture);

                dataRange.AddConditionalFormat()
                    .ColorScale()
                    .LowestValue(XLColor.White)
                    .HighestValue(XLColor.Green);
            }
            else if (allDateTime)
            {
                bool allTimeOnly = true;

                foreach (var cell in dataRange.Cells())
                {
                    var value = cell.Value.ToString();
                    if (DateTime.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                    {
                        cell.Value = time;
                    }
                    else if (DateTime.TryParse(value, out var dateTime))
                    {
                        cell.Value = dateTime;
                        if (dateTime.TimeOfDay == TimeSpan.Zero)
                            allTimeOnly = false;
                    }
                    else
                    {
                        allTimeOnly = false;
                    }
                }

                if (allTimeOnly)
                {
                    dataRange.Style.DateFormat.Format = "HH:mm";
                }
                else
                {
                    dataRange.Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
                }

                dataRange.AddConditionalFormat()
                    .ColorScale()
                    .LowestValue(XLColor.White)
                    .HighestValue(XLColor.Blue);
            }
        }

        workbook.SaveAs(outputPath);
    }
}