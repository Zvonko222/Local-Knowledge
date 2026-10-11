using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using UglyToad.PdfPig;

namespace LocalKnowledgeBase.Core.Services;

/// <summary>
/// 从文件内容提取纯文本。解析全部在本机完成，没有任何网络调用。
/// 支持：pdf（PdfPig 按页解析）、docx（解压读取 word/document.xml）、
/// 其余按 UTF-8 文本读取（txt / md 等）。
/// </summary>
public static class TextExtractor
{
    public static string Extract(Stream stream, string filePath)
    {
        var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
        return extension switch
        {
            ".pdf" => ExtractPdf(stream),
            ".docx" => ExtractDocx(stream),
            _ => ReadAllText(stream)
        };
    }

    /// <summary>用 PdfPig 逐页提取 PDF 文本，页与页之间用空行分隔。</summary>
    public static string ExtractPdf(Stream stream)
    {
        using var document = PdfDocument.Open(stream);
        var builder = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            var text = page.Text;
            if (string.IsNullOrWhiteSpace(text))
                continue;

            if (builder.Length > 0)
                builder.AppendLine().AppendLine();
            builder.Append(text.Trim());
        }
        return builder.ToString();
    }

    private static readonly XNamespace W =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>
    /// 提取 .docx 正文：docx 是 zip 包，正文在 word/document.xml 里。
    /// 按 w:p 段落拆分、拼接段内 w:t 文本。复杂排版元素（表格、脚注等）不在提取范围。
    /// </summary>
    public static string ExtractDocx(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.GetEntry("word/document.xml");
        if (entry is null)
            return string.Empty;

        XDocument xml;
        using (var entryStream = entry.Open())
            xml = XDocument.Load(entryStream);

        var paragraphs = xml.Descendants(W + "p")
            .Select(paragraph => string.Concat(paragraph.Descendants(W + "t").Select(t => t.Value)))
            .Where(text => !string.IsNullOrWhiteSpace(text));

        return string.Join(Environment.NewLine, paragraphs);
    }

    private static string ReadAllText(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
