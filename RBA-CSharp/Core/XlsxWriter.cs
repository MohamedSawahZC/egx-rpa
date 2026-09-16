// =============================================================================
//  XlsxWriter.cs - write .xlsx without Excel (inline strings, UTF-8).
// =============================================================================
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace RbaCore
{
    public static class XlsxWriter
    {
        public static string ToColumnName(int index)
        {
            string name = "";
            while (index > 0)
            {
                int rem = (index - 1) % 26;
                name = ((char)(65 + rem)) + name;
                index = (index - rem - 1) / 26;
            }
            return name;
        }

        public static string XmlSafe(string text)
        {
            if (text == null) { return ""; }
            string t = text.Replace("&", "&amp;");
            t = t.Replace("<", "&lt;");
            t = t.Replace(">", "&gt;");
            t = t.Replace("\"", "&quot;");
            t = Regex.Replace(t, "[\\x00-\\x08\\x0B\\x0C\\x0E-\\x1F]", "");
            return t;
        }

        public static string ExportXlsx(List<OrderedDictionary> rows, string path, string sheetName, bool rightToLeft)
        {
            if (rows == null || rows.Count == 0) { throw new Exception("Export-Xlsx: no rows to write."); }
            List<string> columns = new List<string>();
            foreach (string key in rows[0].Keys) { columns.Add(key); }

            StringBuilder sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            if (rightToLeft)
            {
                sb.Append("<sheetViews><sheetView rightToLeft=\"1\" workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
            }
            else
            {
                sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
            }
            sb.Append("<cols>");
            for (int c = 1; c <= columns.Count; c++)
            {
                sb.Append("<col min=\"" + c + "\" max=\"" + c + "\" width=\"22\" customWidth=\"1\"/>");
            }
            sb.Append("</cols><sheetData>");
            sb.Append("<row r=\"1\">");
            for (int c = 1; c <= columns.Count; c++)
            {
                string cref = ToColumnName(c) + "1";
                string val = XmlSafe(columns[c - 1]);
                sb.Append("<c r=\"" + cref + "\" t=\"inlineStr\" s=\"1\"><is><t>" + val + "</t></is></c>");
            }
            sb.Append("</row>");

            for (int r = 0; r < rows.Count; r++)
            {
                int rowNum = r + 2;
                sb.Append("<row r=\"" + rowNum + "\">");
                for (int c = 0; c < columns.Count; c++)
                {
                    string cref = ToColumnName(c + 1) + rowNum;
                    object raw = rows[r].Contains(columns[c]) ? rows[r][columns[c]] : "";
                    string val = XmlSafe(raw != null ? raw.ToString() : "");
                    if (val != "")
                    {
                        sb.Append("<c r=\"" + cref + "\" t=\"inlineStr\"><is><t xml:space=\"preserve\">" + val + "</t></is></c>");
                    }
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData></worksheet>");
            string sheetXml = sb.ToString();
            string safeSheet = XmlSafe(string.IsNullOrEmpty(sheetName) ? "Sheet1" : sheetName);

            string contentTypes = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                "</Types>";
            string rootRels = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                "</Relationships>";
            string workbook = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                "<sheets><sheet name=\"" + safeSheet + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>";
            string workbookRels = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                "</Relationships>";
            string styles = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Tahoma\"/></font><font><b/><sz val=\"11\"/><name val=\"Tahoma\"/></font></fonts>" +
                "<fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>" +
                "<borders count=\"1\"><border/></borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                "<cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
                "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/></cellXfs></styleSheet>";

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) { Directory.CreateDirectory(dir); }
            if (File.Exists(path)) { File.Delete(path); }

            UTF8Encoding utf8 = new UTF8Encoding(false);
            using (ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                WriteEntry(zip, "[Content_Types].xml", contentTypes, utf8);
                WriteEntry(zip, "_rels/.rels", rootRels, utf8);
                WriteEntry(zip, "xl/workbook.xml", workbook, utf8);
                WriteEntry(zip, "xl/_rels/workbook.xml.rels", workbookRels, utf8);
                WriteEntry(zip, "xl/styles.xml", styles, utf8);
                WriteEntry(zip, "xl/worksheets/sheet1.xml", sheetXml, utf8);
            }
            return Path.GetFullPath(path);
        }

        private static void WriteEntry(ZipArchive zip, string name, string content, Encoding enc)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (Stream stream = entry.Open())
            using (StreamWriter writer = new StreamWriter(stream, enc))
            {
                writer.Write(content);
            }
        }
    }
}
