# BPMAnalyzer
**音频BPM分析控制台程序**  
环境: **.NET 8**（Windows）

## 功能
对wav/mp3文件进行bpm计算

## 使用方法
1. 用脚本注册右键菜单（替代原BPMAnalyzer.Register程序）：
   - 注册：```powershell .\Register-BpmMenu.ps1 -Action Add```
     （弹UAC提权，对所有用户生效；或加 ```-CurrentUser``` 免提权仅当前用户）
   - 移除：```powershell .\Register-BpmMenu.ps1 -Action Remove```
2. 右键wav/mp3文件，选择```Bpm```菜单(win11需要先展开)后会弹出控制台窗口显示结果
3. 也支持命令行输入```BPMAnalyzer xxx.mp3```

## 构建
```bash
dotnet publish .\BPMAnalyzer\BPMAnalyzer.csproj -c Release -r win-x64
```
加 ```-p:PublishAot=true``` 可启用Native AOT，编译为单个免安装.NET运行时的原生exe
（需要VS C++工具链；已实测通过）

## 补充
1. 结果仅作参考，bpm出现过高/过低的情况可考虑将其÷2/×2，变速歌曲分段后再计算
2. 算法来自：[abcsharp/BPMAnalyzer](https://github.com/abcsharp/BPMAnalyzer)，其实我也没看懂，就只是改了音频流处理部分（
3. 使用音频库：[naudio/NAudio](https://github.com/naudio/NAudio)，mp3解码用纯托管库[NLayer](https://github.com/naudio/NLayer)（无interop，兼容Native AOT）

有问题或建议欢迎留issue或b站评论  
下载：[Releases](https://github.com/xyh20180101/BPMAnalyzer/releases)
