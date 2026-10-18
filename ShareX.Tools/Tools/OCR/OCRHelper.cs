#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

#if WINDOWS
using ShareX.HelpersLib;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using Bitmap = SkiaSharp.SKBitmap;
using ImageFormat = SkiaSharp.SKEncodedImageFormat;

namespace ShareX.Tools;

public static class OCRHelper
{
    private const string SupportedVersion = "10.0.18362.0";

    public static bool IsSupported => Helpers.OSVersion >= new Version(SupportedVersion);

    public static OCRLanguageOption[] AvailableLanguages
    {
        get
        {
            ThrowIfNotSupported();
            return OcrEngine.AvailableRecognizerLanguages
                .Select(x => new OCRLanguageOption(x.DisplayName, x.LanguageTag))
                .ToArray();
        }
    }

    public static void ThrowIfNotSupported()
    {
        if (!IsSupported)
        {
            throw new InvalidOperationException(
                string.Format(Localization.Strings.OCRHelper_Requires_Windows_version, SupportedVersion));
        }
    }

    public static async Task<string> OCR(Bitmap bitmap, string languageTag = "en", float scaleFactor = 1f,
        bool singleLine = false)
    {
        ThrowIfNotSupported();
        scaleFactor = Math.Max(scaleFactor, 1f);

        return await Task.Run(async () =>
        {
            using Bitmap scaledBitmap = SkiaImageHelpers.ScaleImageFast(bitmap, scaleFactor);
            return await OCRInternal(scaledBitmap, languageTag, singleLine);
        });
    }

    private static async Task<string> OCRInternal(Bitmap bitmap, string languageTag, bool singleLine)
    {
        Language language = new(languageTag);
        if (!OcrEngine.IsLanguageSupported(language))
        {
            throw new InvalidOperationException(string.Format(Localization.Strings.OCRHelper_Language_unavailable, language.DisplayName));
        }

        OcrEngine engine = OcrEngine.TryCreateFromLanguage(language);
        using InMemoryRandomAccessStream stream = new();
        bitmap.Save(stream.AsStream(), ImageFormat.Png);
        stream.Seek(0);
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
        using SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
        OcrResult result = await engine.RecognizeAsync(softwareBitmap);

        IEnumerable<string> lines;
        if (language.LanguageTag.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ||
            language.LanguageTag.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            lines = result.Lines.Select(line => string.Concat(line.Words.Select(word => word.Text)));
        }
        else if (language.LayoutDirection == LanguageLayoutDirection.Rtl)
        {
            lines = result.Lines.Select(line => string.Join(" ", line.Words.Reverse().Select(word => word.Text)));
        }
        else
        {
            lines = result.Lines.Select(line => line.Text);
        }

        return string.Join(singleLine ? " " : Environment.NewLine, lines);
    }
}
#else
#nullable enable

using ShareX.HelpersLib;
using System.Diagnostics;
using System.Globalization;
using Bitmap = SkiaSharp.SKBitmap;
using ImageFormat = SkiaSharp.SKEncodedImageFormat;

namespace ShareX.Tools;

/// <summary>OCR through the Tesseract command line tool.</summary>
public static class OCRHelper
{
    private static readonly Dictionary<string, string> tesseractLanguageCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ar"] = "ara", ["bg"] = "bul", ["cs"] = "ces", ["da"] = "dan", ["de"] = "deu", ["el"] = "ell",
        ["en"] = "eng", ["es"] = "spa", ["fa"] = "fas", ["fi"] = "fin", ["fr"] = "fra", ["he"] = "heb",
        ["hi"] = "hin", ["hu"] = "hun", ["id"] = "ind", ["it"] = "ita", ["ja"] = "jpn", ["ko"] = "kor",
        ["nb"] = "nor", ["nl"] = "nld", ["pl"] = "pol", ["pt"] = "por", ["ro"] = "ron", ["ru"] = "rus",
        ["sk"] = "slk", ["sv"] = "swe", ["th"] = "tha", ["tr"] = "tur", ["uk"] = "ukr", ["vi"] = "vie",
        ["zh-Hans"] = "chi_sim", ["zh-CN"] = "chi_sim", ["zh-Hant"] = "chi_tra", ["zh-TW"] = "chi_tra"
    };

    private static string? TesseractPath => LinuxEnvironment.FindExecutable("tesseract");

    public static bool IsSupported => TesseractPath != null;

    public static OCRLanguageOption[] AvailableLanguages
    {
        get
        {
            ThrowIfNotSupported();
            return GetInstalledLanguages()
                .Select(code => new OCRLanguageOption(GetDisplayName(code), code))
                .ToArray();
        }
    }

    public static void ThrowIfNotSupported()
    {
        if (!IsSupported)
        {
            throw new InvalidOperationException(Localization.Strings.OCRHelper_Requires_Tesseract);
        }
    }

    public static async Task<string> OCR(Bitmap bitmap, string languageTag = "en", float scaleFactor = 1f,
        bool singleLine = false)
    {
        ThrowIfNotSupported();
        scaleFactor = Math.Max(scaleFactor, 1f);

        return await Task.Run(async () =>
        {
            using Bitmap scaledBitmap = SkiaImageHelpers.ScaleImageFast(bitmap, scaleFactor);
            return await OCRInternal(scaledBitmap, languageTag, singleLine);
        });
    }

    private static async Task<string> OCRInternal(Bitmap bitmap, string languageTag, bool singleLine)
    {
        string language = ToTesseractLanguage(languageTag);
        if (!GetInstalledLanguages().Contains(language, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(string.Format(Localization.Strings.OCRHelper_Language_unavailable, GetDisplayName(language)));
        }

        string imagePath = Path.Combine(Path.GetTempPath(), $"sharex-ocr-{Guid.NewGuid():N}.png");

        try
        {
            using (FileStream fileStream = File.Create(imagePath))
            {
                bitmap.Save(fileStream, ImageFormat.Png);
            }

            string output = await RunTesseractAsync(imagePath, "-", "-l", language, "--psm", singleLine ? "7" : "3");
            IEnumerable<string> lines = output.Replace("\r\n", "\n").Split('\n')
                .Select(line => line.TrimEnd())
                .Where(line => line.Length > 0);

            return string.Join(singleLine ? " " : Environment.NewLine, lines);
        }
        finally
        {
            try { File.Delete(imagePath); } catch { }
        }
    }

    private static string[] GetInstalledLanguages()
    {
        string output = RunTesseractAsync("--list-langs").GetAwaiter().GetResult();
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith("List of available languages", StringComparison.OrdinalIgnoreCase) && line != "osd")
            .ToArray();
    }

    private static string ToTesseractLanguage(string languageTag)
    {
        if (tesseractLanguageCodes.TryGetValue(languageTag, out string? code)) return code;

        int separator = languageTag.IndexOf('-');
        if (separator > 0 && tesseractLanguageCodes.TryGetValue(languageTag[..separator], out code)) return code;

        return languageTag;
    }

    private static string GetDisplayName(string tesseractCode)
    {
        string? languageTag = tesseractLanguageCodes.FirstOrDefault(x => x.Value.Equals(tesseractCode, StringComparison.OrdinalIgnoreCase)).Key;
        if (languageTag != null)
        {
            try
            {
                return CultureInfo.GetCultureInfo(languageTag).DisplayName;
            }
            catch (CultureNotFoundException)
            {
            }
        }

        return tesseractCode;
    }

    private static async Task<string> RunTesseractAsync(params string[] arguments)
    {
        ProcessStartInfo startInfo = new(TesseractPath!)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);

        using Process process = Process.Start(startInfo)!;
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        string output = await outputTask;
        string error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(error.Trim());
        }

        // tesseract --list-langs writes to stderr on older versions.
        return string.IsNullOrWhiteSpace(output) ? error : output;
    }
}
#endif
