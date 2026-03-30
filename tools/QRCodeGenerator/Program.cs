using QRCoder;
using System.Drawing;


PayloadGenerator.Url generator = new PayloadGenerator.Url("https://immich-lounge.github.io");
var payload = generator.ToString();

QRCodeGenerator qrGenerator = new QRCodeGenerator();
QRCodeData qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);

QRCode qrCode = new QRCode(qrCodeData);


var icon = Bitmap.FromFile("../../../../../branding/icon-1024.png") as Bitmap;
Bitmap qrCodeImage = qrCode.GetGraphic(5, Color.Black, Color.White, icon, 30);

qrCodeImage.Save("../../../../../roku/images/qrcode_website.png");