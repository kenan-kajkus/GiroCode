using QRCoder;
using SkiaSharp;
using System.Globalization;
using System.Text;
namespace GiroCode;

public class GiroCodeGenerator : IGiroCodeGenerator
{
    #region constants
    
    private const int StrokeWidth = 5;
    private const int YOffset = 15;
    private const string Br = "\n";
    
    #endregion      
    
    #region fields

    private readonly int _textSize = 20;
    private readonly string _giroCodeText = "Giro-Code";

    #endregion

    public GiroCodeGenerator()
    {
    }
    
    public GiroCodeGenerator(string giroCodeText)
    {
        _giroCodeText = giroCodeText;
    }

    public GiroCodeGenerator(string giroCodeText, int textSize)
    {
        _giroCodeText = giroCodeText;
        _textSize = textSize;
    }

    /// <summary>
    /// Generates the giro code.
    /// </summary>
    /// <param name="beneficiary">The beneficiary.</param>
    /// <param name="iban">The iban.</param>
    /// <param name="remittance">The remittance.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="bic">The bic.</param>
    /// <param name="reference">External reference</param>
    /// <param name="charSet">The character set.</param>
    /// <returns>giro code as byte array</returns>
    public byte[] GenerateGiroCode(
        string beneficiary,
        string iban,
        string remittance,
        decimal amount,
        string? bic,
        string? reference = null,
        CharSet charSet = CharSet.Utf8)
    {
        byte[] qrCode;

        try
        {
            if (string.IsNullOrWhiteSpace(iban))
                throw new Exception("IbanIsEmpty");

            var ibanNormalized = iban.Trim();
            var barcodeContent = GenerateQrCodeContent(beneficiary, ibanNormalized, remittance, amount, bic, reference, charSet: charSet);
            if (!IsBarCodeValid(barcodeContent, charSet))
                throw new Exception("Barcode exceeds binary size limit");

            qrCode = GenerateQrCode(barcodeContent);
        }
        catch (Exception e)
        {
            throw new Exception("QrCodeCreationFailed", e);
        }

        return qrCode;
    }

    /// <summary>
    /// Generates the content of the qr code.
    /// </summary>
    /// <param name="beneficiary">The beneficiary.</param>
    /// <param name="iban">The iban.</param>
    /// <param name="remittance">The remittance.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="bic">The bic.</param>
    /// <param name="reference">The external reference</param>
    /// <param name="charSet">The character set.</param>
    /// <returns>qr code as string</returns>
    private string GenerateQrCodeContent(
        string beneficiary,
        string iban,
        string remittance,
        decimal amount,
        string? bic = null,
        string? reference = null,
        CharSet charSet = CharSet.Utf8)
    {
        var barcodeContentBuilder = new StringBuilder();

        // service tag
        barcodeContentBuilder.Append("BCD").Append(Br);
        // version
        barcodeContentBuilder.Append("002").Append(Br);
        // character set
        barcodeContentBuilder.Append((int)charSet).Append(Br);
        // SEPA Credit Transfer
        barcodeContentBuilder.Append("SCT").Append(Br);
        // Recipient's BIC
        barcodeContentBuilder.Append(bic).Append(Br);
        /* Recipient's name */
        barcodeContentBuilder.Append(beneficiary).Append(Br);
        /* Recipient's IBAN */
        barcodeContentBuilder.Append(iban).Append(Br);
        /* Amount */
        barcodeContentBuilder
            .Append("EUR")
            .Append(amount.ToString("F", CultureInfo.InvariantCulture))
            .Append(Br);
        /* Purpose, optional */
        barcodeContentBuilder.Append("CHAR").Append(Br);
        /* Reference, optional */
        barcodeContentBuilder.Append(reference).Append(Br);
        /* Remittance information */
        barcodeContentBuilder.Append(remittance).Append(Br);
        /* Hint */
        barcodeContentBuilder.Append(string.Empty);

        return barcodeContentBuilder.ToString();
    }

    /// <summary>
    /// Generates Qr code binary
    /// </summary>
    /// <param name="barcodeContent"></param>
    /// <returns></returns>
    private byte[] GenerateQrCode(string barcodeContent)
    {
        var qrGenerator = new QRCodeGenerator();

        // ECC level needs to be "M" per specification
        var qrCodeData = qrGenerator.CreateQrCode(barcodeContent, QRCodeGenerator.ECCLevel.M);
        var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeImage = qrCode.GetGraphic(StrokeWidth);
        var qrBitmap = SKBitmap.FromImage(SKImage.FromBitmap(SKBitmap.Decode(qrCodeImage)));

        // Generate canvas
        var canvasBitmap = new SKBitmap(qrBitmap.Width, qrBitmap.Height + YOffset);
        var canvas = new SKCanvas(canvasBitmap);

        // Set whole canvas white
        canvas.DrawColor(SKColors.White);

        // Text settings
        var textPoint = new SKPoint(qrBitmap.Width / 2f, YOffset - StrokeWidth + (_textSize / 2f));
        var textPaint = new SKPaint
        {
            TextSize = _textSize,
            IsAntialias = false,
            Color = SKColors.Black,
            IsStroke = false,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName(
                "Arial",
                SKFontStyleWeight.Bold,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Italic)
        };

        var qrPoint = new SKPoint(0f, YOffset);

        canvas.DrawBitmap(qrBitmap, qrPoint);

        // Create an SKPaint object to display the frame
        var framePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = StrokeWidth,
            Color = SKColors.Black,
            IsAntialias = true
        };

        canvas.DrawRoundRect(
            StrokeWidth / 2f,
            YOffset - (StrokeWidth / 2f),
            qrBitmap.Width - StrokeWidth,
            qrBitmap.Height,
            20f,
            20f,
            framePaint);

        var textBounds = default(SKRect);
        _ = textPaint.MeasureText(_giroCodeText, ref textBounds);

        textBounds.Left -= StrokeWidth;
        textBounds.Right += StrokeWidth;
        textBounds.Location = new SKPoint(textPoint.X - (textBounds.Width / 2), textPoint.Y - textBounds.Height);

        canvas.ClipRect(textBounds);
        canvas.DrawColor(SKColors.White);

        canvas.DrawText(_giroCodeText, textPoint, textPaint);
        canvas.Flush();

        var resultImage = SKImage.FromBitmap(canvasBitmap);

        var data = resultImage.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }

    /// <summary>
    /// Determines whether [is bar code valid] [the specified barcode content].
    /// </summary>
    /// <param name="barcodeContent">Content of the barcode.</param>
    /// <param name="charSet">The character set.</param>
    /// <returns>
    ///   <c>true</c> if [is bar code valid] [the specified barcode content]; otherwise, <c>false</c>.
    /// </returns>
    private bool IsBarCodeValid(string barcodeContent, CharSet charSet)
    {
        byte[] contentBytes;

        switch (charSet)
        {
            case CharSet.Utf8:
                contentBytes = Encoding.UTF8.GetBytes(barcodeContent);
                break;
            case CharSet.ISO8859_1:
                contentBytes = GetPayloadBytes("ISO-8859-1", barcodeContent);
                break;
            case CharSet.ISO8859_2:
                contentBytes = GetPayloadBytes("ISO-8859-2", barcodeContent);
                break;
            case CharSet.ISO8859_4:
                contentBytes = GetPayloadBytes("ISO-8859-4", barcodeContent);
                break;
            case CharSet.ISO8859_5:
                contentBytes = GetPayloadBytes("ISO-8859-5", barcodeContent);
                break;
            case CharSet.ISO8859_7:
                contentBytes = GetPayloadBytes("ISO-8859-7", barcodeContent);
                break;
            case CharSet.ISO8859_10:
                contentBytes = GetPayloadBytes("ISO-8859-10", barcodeContent);
                break;
            case CharSet.ISO8859_15:
                contentBytes = GetPayloadBytes("ISO-8859-15", barcodeContent);
                break;
            default:
                contentBytes = Array.Empty<byte>();
                break;
        }

        return contentBytes.Length is not (> 331 or 0);
    }

    /// <summary>
    /// Gets the payload bytes.
    /// </summary>
    /// <param name="encoding">The encoding.</param>
    /// <param name="message">The message.</param>
    /// <returns>bitmap as byte array</returns>
    private byte[] GetPayloadBytes(string encoding, string message)
    {
        var iso = Encoding.GetEncoding(encoding);
        var utf8 = Encoding.UTF8;
        var utfBytes = utf8.GetBytes(message);
        return Encoding.Convert(utf8, iso, utfBytes);
    }
}