using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml;
using RepairDesk.Core;

namespace RepairDesk.Desktop;

public static class Exports
{
    public static void SaveOrders(Window owner, IReadOnlyList<RepairOrder> orders)
    {
        var picker = new Microsoft.Win32.SaveFileDialog { Filter = "Книга Excel|*.xlsx", FileName = $"Заказы-{DateTime.Today:yyyy-MM-dd}.xlsx" };
        if (picker.ShowDialog(owner) != true)
            return;
        var rows = new List<string[]> { new[] { "Заказ", "Клиент", "Телефон", "Тип", "Устройство", "Статус", "Мастер", "Принят", "Срок", "Стоимость", "Оплачено" } };
        rows.AddRange(orders.Select(order => new[] { order.Number, order.ClientName, order.Phone, order.DeviceType, order.Device, order.Status, order.Technician, order.CreatedAt.ToString("dd.MM.yyyy"), order.DueAt.ToString("dd.MM.yyyy"), order.Total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), order.Paid.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) }));
        WriteXlsx(picker.FileName, rows, new HashSet<int> { 9, 10 });
        MessageBox.Show("Книга Excel сохранена.", "RepairDesk");
    }

    public static void WriteXlsx(string path, IReadOnlyList<string[]> rows, IReadOnlySet<int>? numericColumns = null)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        Add(archive, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
        Add(archive, "_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        Add(archive, "xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Заказы\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
        Add(archive, "xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
        using var output = archive.CreateEntry("xl/worksheets/sheet1.xml").Open();
        using var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false });
        const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var columnCount = rows.Count == 0 ? 1 : rows.Max(row => row.Length);
        writer.WriteStartElement("worksheet", ns);
        writer.WriteStartElement("sheetViews", ns);
        writer.WriteStartElement("sheetView", ns);
        writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteStartElement("pane", ns);
        writer.WriteAttributeString("ySplit", "1");
        writer.WriteAttributeString("topLeftCell", "A2");
        writer.WriteAttributeString("activePane", "bottomLeft");
        writer.WriteAttributeString("state", "frozen");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("cols", ns);
        writer.WriteStartElement("col", ns);
        writer.WriteAttributeString("min", "1");
        writer.WriteAttributeString("max", columnCount.ToString());
        writer.WriteAttributeString("width", "24");
        writer.WriteAttributeString("customWidth", "1");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("sheetData", ns);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            writer.WriteStartElement("row", ns);
            writer.WriteAttributeString("r", (rowIndex + 1).ToString());
            for (var column = 0; column < rows[rowIndex].Length; column++)
            {
                writer.WriteStartElement("c", ns);
                writer.WriteAttributeString("r", $"{(char)('A' + column)}{rowIndex + 1}");
                if (rowIndex > 0 && numericColumns?.Contains(column) == true && decimal.TryParse(rows[rowIndex][column], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount))
                {
                    writer.WriteAttributeString("t", "n");
                    writer.WriteElementString("v", ns, amount.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                else
                {
                    writer.WriteAttributeString("t", "inlineStr");
                    writer.WriteStartElement("is", ns);
                    writer.WriteElementString("t", ns, rows[rowIndex][column]);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteStartElement("autoFilter", ns);
        writer.WriteAttributeString("ref", $"A1:{(char)('A' + columnCount - 1)}{Math.Max(1, rows.Count)}");
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void Add(ZipArchive archive, string path, string contents)
    {
        using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(false));
        writer.Write(contents);
    }

    public static void PrintOrder(Window owner, RepairOrder order, IReadOnlyList<OrderLine> lines)
    {
        var document = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 12, PagePadding = new Thickness(48), ColumnWidth = 1000, Foreground = Brushes.Black, Background = Brushes.White };
        void Paragraph(string text, double size = 12, bool bold = false) => document.Blocks.Add(new Paragraph(new Run(text)) { FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Margin = new Thickness(0, 0, 0, 12) });
        Paragraph("RepairDesk", 25, true);
        Paragraph($"Квитанция / заказ {order.Number}", 18, true);
        Paragraph($"Дата приема: {order.CreatedAt:dd.MM.yyyy HH:mm}\nПлановый срок: {order.DueAt:dd.MM.yyyy}\nСтатус: {order.Status}");
        Paragraph($"Клиент: {order.ClientName}\nТелефон: {order.Phone}");
        Paragraph($"Устройство: {order.DeviceType} · {order.Device}\nСерийный номер: {order.Serial}", 13, true);
        Paragraph("Заявленная неисправность: " + order.Complaint);
        Paragraph("Состояние и комплектность: " + order.Condition);
        Paragraph("Результат диагностики: " + (order.Diagnosis.Length == 0 ? "Не проведена" : order.Diagnosis));
        var table = new Table { CellSpacing = 0 };
        table.Columns.Add(new TableColumn { Width = new GridLength(330) });
        table.Columns.Add(new TableColumn { Width = new GridLength(60) });
        table.Columns.Add(new TableColumn { Width = new GridLength(100) });
        var group = new TableRowGroup();
        table.RowGroups.Add(group);
        void Row(params string[] values)
        {
            var row = new TableRow();
            foreach (var value in values)
                row.Cells.Add(new TableCell(new Paragraph(new Run(value))) { Padding = new Thickness(7), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 1) });
            group.Rows.Add(row);
        }
        Row("Работа / запчасть", "Кол-во", "Сумма");
        foreach (var line in lines)
            Row(line.Name, line.Quantity.ToString(), line.TotalText);
        document.Blocks.Add(table);
        Paragraph($"Итого: {order.TotalText}\nОплачено: {Money.Format(order.Paid)}\nК оплате: {Money.Format(order.AmountDue)}", 15, true);
        if (order.Status == "Отменен")
            Paragraph("Заказ отменен. Состав сохранен для истории; запчасти возвращены на склад.");
        Paragraph("Данные принятых устройств и перечень согласованных работ проверены.");
        Paragraph("Сотрудник ____________________    Клиент ____________________\nДата _________________________");
        var preview = new Window { Owner = owner, Title = $"Квитанция {order.Number}", Width = 850, Height = 850, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        preview.Content = new FlowDocumentReader { Document = document, ViewingMode = FlowDocumentReaderViewingMode.Scroll, IsFindEnabled = true, IsPrintEnabled = true };
        preview.ShowDialog();
    }
}
