<#
.SYNOPSIS
    注册/移除 BPMAnalyzer 的 "Bpm" 右键菜单（替代 BPMAnalyzer.Register WPF 程序）。

.DESCRIPTION
    逻辑与 BPMAnalyzer.Register 的 MainWindow.xaml.cs 完全一致：
      1. 检查管理员权限（写 HKCR 需要；非管理员自动弹 UAC 提权重启本脚本）
      2. Add: 创建 HKCR\*\shell\Bpm，设置 AppliesTo=".wav OR .mp3"，
             并创建 command 子键，默认值为 "<exe路径>" "%1"
      3. Remove: 删除 HKCR\*\shell\Bpm
    改进点：
      * 使用脚本同目录的 BPMAnalyzer.exe 绝对路径（原程序用 Environment.CurrentDirectory，
        从别的目录启动会注册错路径）
      * -CurrentUser 模式写入 HKCU\Software\Classes，无需管理员权限即可生效
      * -Quiet 模式不弹 MessageBox，适合脚本调用
      * 不带 -Action 参数运行时弹出菜单选择 Add/Remove（等效原 GUI 的按钮）

.EXAMPLE
    .\Register-BpmMenu.ps1                        # 交互菜单选择
    .\Register-BpmMenu.ps1 -Action Add          # 注册（弹 UAC 提权）
    .\Register-BpmMenu.ps1 -Action Add -CurrentUser   # 仅当前用户，免提权
    .\Register-BpmMenu.ps1 -Action Remove       # 移除
#>
[CmdletBinding()]
param(
    [ValidateSet('Add', 'Remove')]
    [string]$Action,

    [switch]$CurrentUser,

    [switch]$Quiet
)

# ---------- 交互模式：不带 -Action 时弹出菜单（等效原 GUI 的 Add/Remove 按钮） ----------
if (-not $Action) {
    Write-Host ''
    Write-Host 'BPMAnalyzer 右键菜单管理'
    Write-Host '  1) Add     注册 Bpm 右键菜单'
    Write-Host '  2) Remove  移除 Bpm 右键菜单'
    $choice = Read-Host '请选择 (1/2)'
    switch ($choice) {
        '1' { $Action = 'Add' }
        '2' { $Action = 'Remove' }
        default { Write-Host '无效输入，已退出。'; exit 1 }
    }
}

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

function Show-Msg {
    param([string]$Text)
    if ($Quiet) { Write-Host $Text }
    else { [System.Windows.Forms.MessageBox]::Show($Text) | Out-Null }
}

function Test-IsAdmin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

# ---------- 定位 BPMAnalyzer.exe ----------
$exePath = Join-Path $PSScriptRoot 'BPMAnalyzer.exe'
if (-not (Test-Path $exePath -PathType Leaf)) {
    $exePath = Join-Path (Get-Location) 'BPMAnalyzer.exe'
}

# ---------- 权限检查与自提权 ----------
if (-not $CurrentUser -and -not (Test-IsAdmin)) {
    $argList = "-NoProfile -ExecutionPolicy Bypass -File `"$($MyInvocation.MyCommand.Path)`" -Action $Action"
    if ($CurrentUser) { $argList += ' -CurrentUser' }
    if ($Quiet) { $argList += ' -Quiet' }
    Start-Process -FilePath 'powershell.exe' -ArgumentList $argList -Verb RunAs
    exit 0
}

# ---------- 注册表操作（与 WPF 相同 API：OpenSubKey/CreateSubKey/SetValue/DeleteSubKeyTree） ----------
if ($CurrentUser) {
    $root = [Microsoft.Win32.Registry]::CurrentUser
    $shellPath = 'Software\Classes\*\shell'
} else {
    $root = [Microsoft.Win32.Registry]::ClassesRoot
    $shellPath = '*\shell'
}

try {
    if ($Action -eq 'Add') {
        $commandLine = "`"$exePath`" `"%1`""

        $shellKey = $root.OpenSubKey($shellPath, $true)
        if ($null -eq $shellKey) { $shellKey = $root.CreateSubKey($shellPath) }

        $bpmKey = $shellKey.CreateSubKey('Bpm')
        $bpmKey.SetValue('AppliesTo', '.wav OR .mp3')

        $commandKey = $bpmKey.CreateSubKey('command')
        $commandKey.SetValue('', $commandLine)

        $bpmKey.Close()
        $shellKey.Close()

        Show-Msg 'Add Successfully.'
    }
    else {
        $shellKey = $root.OpenSubKey($shellPath, $true)
        if ($null -ne $shellKey) {
            $shellKey.DeleteSubKeyTree('Bpm', $false)
            $shellKey.Close()
        }
        Show-Msg 'Remove Successfully.'
    }
}
catch {
    Show-Msg "Error: $($_.Exception.Message)"
    exit 1
}
