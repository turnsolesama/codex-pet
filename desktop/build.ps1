$ErrorActionPreference = 'Stop'
$petRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler is unavailable.' }
$outputFolder = Join-Path $petRoot 'releases\desktop-v0.4.1'
New-Item -ItemType Directory -Path $outputFolder -Force | Out-Null
$output = Join-Path $outputFolder 'NatsumePet.exe'
$arguments = @('/nologo', '/target:winexe', '/optimize+', '/platform:anycpu', '/codepage:65001', "/out:$output", '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll')
$assets = @{
    'standard.idle' = 'assets\natsume\restoration-v1\outfits\maid.png'
    'standard.closed' = 'assets\natsume\animation-v1\standard-closed.png'
    'standard.annoyed' = 'assets\natsume\animation-v1\standard-annoyed.png'
    'standard.happy' = 'assets\natsume\interaction-v1\standard-happy.png'
    'standard.shy' = 'assets\natsume\interaction-v1\standard-shy.png'
    'big-head.idle' = 'assets\natsume\body-types-v1\big-head.png'
    'big-head.closed' = 'assets\natsume\animation-v1\big-head-closed.png'
    'big-head.annoyed' = 'assets\natsume\animation-v1\big-head-annoyed.png'
    'big-head.happy' = 'assets\natsume\interaction-v1\big-head-happy.png'
    'big-head.shy' = 'assets\natsume\interaction-v1\big-head-shy.png'
}
foreach ($key in $assets.Keys) { $arguments += "/resource:$(Join-Path $petRoot $assets[$key]),Sprite.$key" }
$poses = @{ sit='a02-sit'; sleep='a03-sleep'; pickup='d01-pickup'; drag='d02-drag'; land='d03-land' }
foreach ($pose in $poses.Keys) {
    $posePath = Join-Path $petRoot "assets\natsume\restoration-v1\motions\$($poses[$pose]).png"
    $arguments += "/resource:$posePath,Pose.$pose"
}
foreach ($variant in @('standard', 'big-head')) {
    foreach ($gripOutfit in @('maid', 'casual', 'pink-waitress', 'winter-coat', 'red-cheongsam')) {
        $gripPath = Join-Path $petRoot "assets\natsume\edge-grip-v2\$variant\$gripOutfit.png"
        if (-not (Test-Path -LiteralPath $gripPath)) { throw "Missing edge grip: $gripPath" }
        $arguments += "/resource:$gripPath,Grip.$variant.$gripOutfit"
    }
    foreach ($outfit in @('casual', 'pink-waitress', 'winter-coat', 'red-cheongsam')) {
        foreach ($expression in @('idle', 'closed', 'annoyed', 'happy', 'shy')) {
            $assetPath = Join-Path $petRoot "assets\natsume\wardrobe-runtime-v1\$variant\$outfit\$expression.png"
            if (-not (Test-Path -LiteralPath $assetPath)) { throw "Missing wardrobe frame: $assetPath" }
            $arguments += "/resource:$assetPath,Wardrobe.$variant.$outfit.$expression"
        }
    }
}
$arguments += (Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs').FullName
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $LASTEXITCODE" }
$packageNotes = @'
枣子姐 Q 版桌宠 0.4.1

双击 NatsumePet.exe 启动，无需安装。Windows / .NET Framework 4.x。
右键小人或托盘图标打开菜单；“退出”关闭。

点头发：开心；点脸：害羞；点衣服：嫌弃；点脚：轻跳。
连续点三次会嫌弃。按住拖动，松手落地回弹。
菜单可切换两种 Q 版、尺寸、睡觉、置顶、暂停。
右键 → 换装：女仆装、日常私服、粉色服务员装、冬日外套、红金旗袍。
两种 Q 版的每套衣服均支持待机、闭眼、开心、害羞和嫌弃。
拖到屏幕左右边缘或角落附近松手，自动收纳，露出连贯的头颈肩，双手抓住屏幕边框探头。
点击探头可以展开，按住可拖出来；拖远后再靠边才能再次自动收纳。
收纳后的待机保持稳定，不再整张图左右伸缩。
右键菜单仍可手动选择左侧 / 右侧收纳。
标准 Q 版女仆装还保留坐下、打盹、拎起、拖动及落地原动作稿。

等身版等待官方素材。程序不联网，不添加开机启动。
当前使用表情帧与整体变换动画，后续可继续细化。
'@
Set-Content -LiteralPath (Join-Path $outputFolder '使用说明.txt') -Value $packageNotes -Encoding UTF8
$packageFile = Get-Item -LiteralPath $output
$packageHash = Get-FileHash -LiteralPath $output -Algorithm SHA256
@{ version = '0.4.1'; file = $packageFile.Name; bytes = $packageFile.Length; sha256 = $packageHash.Hash; runtime = 'Windows .NET Framework 4.x'; published = $false } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputFolder 'package.json') -Encoding UTF8
Get-Item -LiteralPath $output | Format-List FullName, Length
Get-FileHash -LiteralPath $output -Algorithm SHA256 | Format-List Algorithm, Hash


