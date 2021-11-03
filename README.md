# BPMAnalyzer
**音频BPM分析控制台程序**  
(附windows添加右键菜单脚本)  
环境: **.net core 3.1**

## 功能
对wav/mp3文件进行bpm计算

## 使用方法
1. 右键wav/mp3文件，选择```Bpm```菜单(win11需要先展开)后会弹出控制台窗口显示结果
2. 命令行输入```BPMAnalyzer xxx.mp3``` 

## 补充
1. 结果仅作参考，bpm出现过高/过低的情况可考虑将其÷2/×2，变速歌曲分段后再计算
2. bat脚本正常情况下会停留且提示成功，如出现闪退/提示无权限，请尝试使用管理员模式或通过终端调用bat
3. 算法来自[abcsharp/BPMAnalyzer](https://github.com/abcsharp/BPMAnalyzer)，其实我也没看懂，就只是改了音频流处理部分（


有问题或建议欢迎留issue或b站评论
