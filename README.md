# BPMAnalyzer
**音频BPM分析控制台程序**  
(附windows添加右键菜单工具)  
环境: **.net core 3.1**

## 功能
对wav/mp3文件进行bpm计算

## 使用方法
1. 运行```BPMAnalyzer.Register.exe```，点Add添加右键菜单，点Remove移除右键菜单
2. 右键wav/mp3文件，选择```Bpm```菜单(win11需要先展开)后会弹出控制台窗口显示结果
3. 也支持命令行输入```BPMAnalyzer xxx.mp3``` 

## 补充
1. 结果仅作参考，bpm出现过高/过低的情况可考虑将其÷2/×2，变速歌曲分段后再计算
2. 算法来自：[abcsharp/BPMAnalyzer](https://github.com/abcsharp/BPMAnalyzer)，其实我也没看懂，就只是改了音频流处理部分（
3. 使用音频库：[naudio/NAudio](https://github.com/naudio/NAudio)


有问题或建议欢迎留issue或b站评论  
下载：[Releases](https://github.com/xyh20180101/BPMAnalyzer/releases)
