param([switch]$IncludeOfficial)

$ErrorActionPreference = 'Stop'
$petRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler is unavailable.' }
$releaseName = if ($IncludeOfficial) { 'desktop-v0.5.9-official' } else { 'desktop-v0.5.9' }
$officialAssets = @{}
if ($IncludeOfficial) {
    $missingOfficial = @()
    foreach ($outfit in @('maid', 'casual', 'pink-waitress', 'winter-coat', 'red-cheongsam')) {
        foreach ($expression in @('idle', 'closed', 'annoyed', 'happy', 'shy')) {
            $officialPath = Join-Path $petRoot "assets\natsume\official-runtime-hd-v1\$outfit\$expression.png"
            if (-not (Test-Path -LiteralPath $officialPath -PathType Leaf)) { $missingOfficial += $officialPath }
            $officialAssets["Official.$outfit.$expression"] = $officialPath
        }
    }
    if ($missingOfficial.Count -gt 0) { throw ("官方等身素材必须完整包含五套衣装 × 五种表情，缺少：`n" + ($missingOfficial -join "`n")) }
    foreach ($outfit in @('maid','casual','pink-waitress','winter-coat','red-cheongsam')) {
        foreach ($pose in @('sit','wave','pickup','grip')) {
            $motionPath=Join-Path $petRoot "assets\natsume\official-motion-hd-v1\$outfit\$pose.png"
            if (-not (Test-Path -LiteralPath $motionPath)) { throw "Missing official-derived motion: $motionPath" }
            $officialAssets["OfficialMotion.$outfit.$pose"]=$motionPath
        }
    }
    foreach ($pose in @('sit','wave','pickup')) {
        $updoPath=Join-Path $petRoot "assets\natsume\official-motion-hd-v1\red-cheongsam-updo\$pose.png"
        if (-not (Test-Path -LiteralPath $updoPath)) { throw "Missing cheongsam updo motion: $updoPath" }
        $officialAssets["OfficialMotion.red-cheongsam-updo.$pose"]=$updoPath
    }
    $officialAssets['OfficialMotion.Meta']=Join-Path $petRoot 'assets\natsume\official-motion-restored-v1\metrics.txt'
    foreach ($pose in @('sit','wave','pickup')) {
        $classicPath=Join-Path $petRoot "assets\natsume\official-motion-restored-v1\maid-classic\$pose.png"
        if (-not (Test-Path -LiteralPath $classicPath)) { throw "Missing maid classic face motion: $classicPath" }
        $officialAssets["OfficialMotion.maid-classic.$pose"]=$classicPath
    }
}
$outputFolder = Join-Path $petRoot "releases\$releaseName"
New-Item -ItemType Directory -Path $outputFolder -Force | Out-Null
$output = Join-Path $outputFolder 'NatsumePet.exe'
$arguments = @('/nologo', '/target:winexe', '/optimize+', '/platform:anycpu', '/codepage:65001', "/out:$output", '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll')
$iconPath = Join-Path $petRoot 'assets\natsume\app-icon-v1\icon.ico'
if (-not (Test-Path -LiteralPath $iconPath)) { throw "Missing app icon: $iconPath" }
$arguments += "/win32icon:$iconPath"
$arguments += "/resource:$iconPath,App.Icon"
foreach ($resourceName in $officialAssets.Keys) { $arguments += "/resource:$($officialAssets[$resourceName]),$resourceName" }
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
        $gripVersion = if ($gripOutfit -eq 'maid') { 'edge-grip-v3' } else { 'edge-grip-v2' }
        $gripPath = Join-Path $petRoot "assets\natsume\$gripVersion\$variant\$gripOutfit.png"
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
枣子姐桌宠 0.5.9

双击 NatsumePet.exe 启动，无需安装。Windows / .NET Framework 4.x。
右键小人或托盘图标打开分组菜单；最下面的“退出桌宠”关闭。

点头发：开心；点脸：害羞；点衣服：嫌弃；点脚：轻跳。
连续点三次会嫌弃。默认松手后停在当前位置，坐姿和睡姿拖动时保留。右键 → 额外功能 → 重力下落可开启落地效果，默认关闭。
右键 → 表情与动作：开心、害羞、嫌弃、轻跳及睡觉；Q 版坐下仅限标准 Q 版女仆装；等身版五套服饰均支持坐下。
右键 → 体型与大小：切换体型及四档尺寸；主菜单可置顶、暂停。默认标准 Q 版，两种 Q 版均保留。新增超大号，比大号增加 20%：Q 版 432，等身版 576；全身、动作与抓框同步缩放。
暂停只冻结自动动画，主动点击或拖动会恢复互动。
从收纳状态选表情、跳跃、睡觉或坐下，会展开并稳定回应；默认不自动落到底部。
右键 → 换装：女仆装、日常私服、粉色服务员装、冬日外套、红金旗袍。
两种 Q 版的每套衣服均支持待机、闭眼、开心、害羞和嫌弃。
拖到屏幕左右边缘或角落附近松手，自动收纳；两种 Q 版露出连贯的头颈肩，双手抓住屏幕边框探头。
点击探头可以展开；按住沿侧边上下拖动可调整位置，松手保持收纳。
向屏幕内横拖约 32–50 像素才展开；拖远后再靠边才能再次自动收纳。
Q 版女仆装抓框贴图重新绘制，裁切对齐握框线，部分手指藏到屏幕外，去掉侧边白色空隙。
0.4.5 小号由 200 调为 160，中号由 280 调为 240，大号保持 360；全身与边缘探头同比缩小。默认使用中号。
收纳后的待机保持稳定，不再整张图左右伸缩。
右键 → 收纳到边缘：Q 版显示左侧抓框 / 右侧抓框。
标准 Q 版女仆装还保留坐下、打盹、拎起、拖动及落地原动作稿。
应用文件、窗口和托盘使用统一灰底 Q 版头像图标。
0.4.3 修复高 DPI 下透明窗口画面停留在首帧的问题，动画与互动结果可实时显示。

程序不联网，不添加开机启动。
当前使用表情帧与整体变换动画，后续可继续细化。
'@
if ($IncludeOfficial) {
    $packageNotes += "`r`n`r`n0.5.9：修复坐姿睡眠时脱边或头部出屏、菜单收纳时上跳、拖动捕获中断后贴边偏移；四档尺寸含超大号均加入回归。"
    $packageNotes += "`r`n`r`n0.5.6：女仆常态脸坐下、招手、拎起三张动作经 AI 精修去噪，透明边缘与显示比例同步校验。萌萌脸仍为单独选项，站姿、扒窗及其他服饰沿用原素材。默认无重力、常驻姿势与拖动保留。"
    $packageNotes += "`r`n`r`n0.5.4：等身版三档按约 4/3 放大为 213 / 320 / 480，启动默认 480；站姿、坐姿、招手、拎起和边缘抓框同步缩放。切回 Q 版保留对应 160 / 240 / 360 档位。v6 女仆修复候选继续单独保留。`r`n`r`n0.5.2–0.5.3：旗袍坐姿、招手、拎起默认使用盘发，右键 → 换装 → 旗袍动作造型可切换到保留的长发款；站姿和抓框共用原盘发图。女仆动作与抓框的白布包发收小并贴合后脑。"
    $packageNotes += "`r`n`r`n0.5.5 等身桌宠：五套衣装各有坐下、招手、拎起和专用双手抓框图，按官方形象生成衍生动作；原游戏立绘与五种表情继续保留。`r`n等身模式启动为坐姿，拖动保留坐姿并停在放置位置。右键 → 表情与动作 → 常驻姿势可选站立、坐下、招手和拎起，点击回应结束后恢复所选姿势。右键表情与动作可坐下休息或站起来显示官方原立绘。`r`n女仆装头发扎起并用白布包住后脑发髻。`r`n贴边拖动按可见人物计算，透明窗口留边不会阻止靠近屏幕；收纳后双手抓框，可上下滑动和向内拖出，三档尺寸同比变化。`r`n命令行：NatsumePet.exe --variant official --outfit casual。`r`n动作采用姿势切换和小幅整体变换，尚非骨骼或完整逐帧动画。含用户本地官方素材，仅本地使用，不公开上传。"
} else {
    $packageNotes += "`r`n`r`nbuild.ps1 默认生成本版本，仅包含两种 Q 版，不嵌入任何私有官方等身素材。官方等身版需另行使用 -IncludeOfficial 构建的私有本地包，该包不公开发布。"
}
Set-Content -LiteralPath (Join-Path $outputFolder '使用说明.txt') -Value $packageNotes -Encoding UTF8
$packageFile = Get-Item -LiteralPath $output
$packageHash = Get-FileHash -LiteralPath $output -Algorithm SHA256
@{ version = '0.5.9'; file = $packageFile.Name; bytes = $packageFile.Length; sha256 = $packageHash.Hash; runtime = 'Windows .NET Framework 4.x'; published = $false; privateOfficialAssets = [bool]$IncludeOfficial } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputFolder 'package.json') -Encoding UTF8
Get-Item -LiteralPath $output | Format-List FullName, Length
Get-FileHash -LiteralPath $output -Algorithm SHA256 | Format-List Algorithm, Hash
