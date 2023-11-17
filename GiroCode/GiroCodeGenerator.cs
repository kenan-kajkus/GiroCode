using QRCoder;
using SkiaSharp;
using System.Globalization;
using System.Text;
namespace GiroCode;

public class GiroCodeGenerator : IGiroCodeGenerator
{
    #region Fields

    private readonly int _strokeWidth = 5;
    private readonly int _yOffset = 15;
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
    /// <param name="charSet">The character set.</param>
    /// <returns>giro code as byte array</returns>
    public byte[] GenerateGiroCode(
        string beneficiary,
        string iban,
        string remittance,
        decimal amount,
        string bic,
        CharSet charSet = CharSet.Utf8)
    {
        byte[] qrCode;

        try
        {
            if (string.IsNullOrWhiteSpace(iban))
                throw new Exception("IbanIsEmpty");

            var ibanNormalized = iban.Trim();
            var barcodeContent = GenerateQrCodeContent(beneficiary, ibanNormalized, remittance, amount, bic, charSet: charSet);
            if (!IsBarCodeValid(barcodeContent, charSet))
                throw new Exception("IbanIsInvalid");

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
    /// <param name="charSet">The character set.</param>
    /// <returns>qr code as string</returns>
    private string GenerateQrCodeContent(
        string beneficiary,
        string iban,
        string remittance,
        decimal amount,
        string bic = null,
        CharSet charSet = CharSet.Utf8)
    {
        var barcodeContent =
            /* Service tag */
            "BCD\r\n" +
            /* Version */
            "002\r\n" +
            /* Character set */
            $"{(int)charSet}\r\n" +
            /* SEPA Credit Transfer */
            "SCT\r\n" +
            /* Recipient's BIC */
            $"{bic}\r\n" +
            /* Recipient's name */
            $"{beneficiary}\r\n" +
            /* Recipient's IBAN */
            $"{iban}\r\n" +
            /* Amount */
            $"EUR{amount.ToString("F", CultureInfo.InvariantCulture)}\r\n" +
            /* Purpose, optional */
            "CHAR\r\n" +
            /* Reference, optional */
            "\r\n" +
            /* Remittance information */
            $"{remittance}\r\n" +
            /* Hint */
            string.Empty;

        return barcodeContent;
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
        var qrCodeImage = qrCode.GetGraphic(_strokeWidth);
        var qrBitmap = SKBitmap.FromImage(SKImage.FromBitmap(SKBitmap.Decode(qrCodeImage)));

        // Generate canvas
        var canvasBitmap = new SKBitmap(qrBitmap.Width, qrBitmap.Height + _yOffset);
        var canvas = new SKCanvas(canvasBitmap);

        // Set whole canvas white
        canvas.DrawColor(SKColors.White);

        // Text settings
        var textPoint = new SKPoint(qrBitmap.Width / 2f, _yOffset - _strokeWidth + (_textSize / 2));
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

        var qrPoint = new SKPoint(0f, _yOffset);

        canvas.DrawBitmap(qrBitmap, qrPoint);

        // Create an SKPaint object to display the frame
        var framePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = _strokeWidth,
            Color = SKColors.Black,
            IsAntialias = true
        };

        canvas.DrawRoundRect(
            _strokeWidth / 2f,
            _yOffset - (_strokeWidth / 2f),
            qrBitmap.Width - _strokeWidth,
            qrBitmap.Height,
            20f,
            20f,
            framePaint);

        var textBounds = default(SKRect);
        _ = textPaint.MeasureText(_giroCodeText, ref textBounds);

        textBounds.Left -= _strokeWidth;
        textBounds.Right += _strokeWidth;
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
    /// <returns>bitman as byte array</returns>
    private byte[] GetPayloadBytes(string encoding, string message)
    {
        var iso = Encoding.GetEncoding(encoding);
        var utf8 = Encoding.UTF8;
        var utfBytes = utf8.GetBytes(message);
        return Encoding.Convert(utf8, iso, utfBytes);
    }
}