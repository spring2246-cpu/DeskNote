Add-Type -AssemblyName System.Drawing
$assetRoot=Join-Path $PSScriptRoot 'assets'
function RoundPath([single]$x,[single]$y,[single]$w,[single]$h,[single]$r){
 $p=[Drawing.Drawing2D.GraphicsPath]::new();$d=$r*2
 $p.AddArc($x,$y,$d,$d,180,90);$p.AddArc($x+$w-$d,$y,$d,$d,270,90);$p.AddArc($x+$w-$d,$y+$h-$d,$d,$d,0,90);$p.AddArc($x,$y+$h-$d,$d,$d,90,90);$p.CloseFigure();return ,$p
}
$frames=@()
foreach($size in @(16,20,24,32,40,48,64,128,256)){
 $large=[Drawing.Bitmap]::new($size*4,$size*4);$g=[Drawing.Graphics]::FromImage($large);$g.SmoothingMode='AntiAlias';$g.ScaleTransform($size/4.0,$size/4.0)
 $tile=RoundPath 0.5 0.5 15 15 3.6
 $gradient=[Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.PointF]::new(0,0),[Drawing.PointF]::new(16,16),[Drawing.ColorTranslator]::FromHtml('#35C7F4'),[Drawing.ColorTranslator]::FromHtml('#1465CE'));$g.FillPath($gradient,$tile)
 $paper=RoundPath 4 2.8 8 10.4 1.2;$white=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#F5FCFF'));$g.FillPath($white,$paper)
 $ink=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#247EBB'),1);$ink.StartCap='Round';$ink.EndCap='Round';$g.DrawLine($ink,6,5.5,10,5.5);$g.DrawLine($ink,6,8,8.5,8)
 $rim=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#1471BE'));$g.FillEllipse($rim,8.1,8.1,7.2,7.2)
 $mint=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#73F0CB'));$g.FillEllipse($mint,8.7,8.7,6,6)
 $check=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#075C65'),1.15);$check.StartCap='Round';$check.EndCap='Round';$check.LineJoin='Round';$g.DrawLines($check,[Drawing.PointF[]]@([Drawing.PointF]::new(10.1,11.7),[Drawing.PointF]::new(11.2,12.8),[Drawing.PointF]::new(13.2,10.6)))
 $g.Dispose();$bitmap=[Drawing.Bitmap]::new($size,$size);$out=[Drawing.Graphics]::FromImage($bitmap);$out.InterpolationMode='HighQualityBicubic';$out.DrawImage($large,0,0,$size,$size);$out.Dispose()
 $memory=[IO.MemoryStream]::new();$bitmap.Save($memory,[Drawing.Imaging.ImageFormat]::Png);$frames+=,@{Size=$size;Bytes=$memory.ToArray()}
 if($size -eq 256){$bitmap.Save((Join-Path $assetRoot 'DeskNote.png'),[Drawing.Imaging.ImageFormat]::Png)}
 $memory.Dispose();$bitmap.Dispose();$large.Dispose();$tile.Dispose();$gradient.Dispose();$paper.Dispose();$white.Dispose();$ink.Dispose();$rim.Dispose();$mint.Dispose();$check.Dispose()
}
$stream=[IO.File]::Create((Join-Path $assetRoot 'DeskNote.ico'));$writer=[IO.BinaryWriter]::new($stream);$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count);$offset=6+16*$frames.Count
foreach($frame in $frames){$dimension=if($frame.Size -eq 256){0}else{$frame.Size};$writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frame.Bytes.Length);$writer.Write([uint32]$offset);$offset+=$frame.Bytes.Length}
foreach($frame in $frames){$writer.Write([byte[]]$frame.Bytes)};$writer.Dispose();$stream.Dispose()
