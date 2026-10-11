using System.IO.Compression;
using System.Text;
using LocalKnowledgeBase.Core.Services;

namespace LocalKnowledgeBase.Core.Tests;

/// <summary>
/// TextExtractor 的提取逻辑测试。PDF/docx 样例都在测试内程序化构造，不依赖外部文件。
/// </summary>
public sealed class TextExtractorTests
{
    [Fact]
    public void ExtractDocx_JoinsParagraphText()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("word/document.xml");
            using var writer = new StreamWriter(entry.Open());
            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
  <w:body>
    <w:p><w:r><w:t>第一段：项目决策记录</w:t></w:r></w:p>
    <w:p><w:r><w:t>第二段：使用 xUnit 编写测试</w:t></w:r></w:p>
  </w:body>
</w:document>");
        }
        stream.Position = 0;

        var text = TextExtractor.ExtractDocx(stream);

        Assert.Contains("第一段：项目决策记录", text);
        Assert.Contains("第二段：使用 xUnit 编写测试", text);
        // 段落之间应有换行
        Assert.Contains(Environment.NewLine, text);
    }

    [Fact]
    public void ExtractDocx_MissingDocumentXml_ReturnsEmpty()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            zip.CreateEntry("not-the-right-entry.xml");
        }
        stream.Position = 0;

        var text = TextExtractor.ExtractDocx(stream);

        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void ExtractPdf_ReadsTextFromMinimalPdf()
    {
        var pdfBytes = BuildMinimalPdf("Hello Knowledge Base");
        using var stream = new MemoryStream(pdfBytes);

        var text = TextExtractor.ExtractPdf(stream);

        Assert.Contains("Hello Knowledge Base", text);
    }

    [Fact]
    public void Extract_DispatchesByExtension()
    {
        var docxBytes = BuildMinimalDocx("来自扩展名分发的段落");
        using var stream = new MemoryStream(docxBytes);

        var text = TextExtractor.Extract(stream, "notes.docx");

        Assert.Contains("来自扩展名分发的段落", text);
    }

    /// <summary>程序化构造一个最小合法 PDF（目录 + 页 + 内容流 + Helvetica 字体）。</summary>
    private static byte[] BuildMinimalPdf(string text)
    {
        var content = $"BT /F1 16 Tf 72 720 Td ({text}) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] " +
            "/Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        foreach (var body in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append($"{offsets.Count} 0 obj\n{body}\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            pdf.Append($"{offset:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    /// <summary>程序化构造一个最小 docx（zip + word/document.xml）。</summary>
    private static byte[] BuildMinimalDocx(string paragraphText)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("word/document.xml");
            using var writer = new StreamWriter(entry.Open());
            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
  <w:body><w:p><w:r><w:t>" + paragraphText + @"</w:t></w:r></w:p></w:body>
</w:document>");
        }
        return stream.ToArray();
    }
}
