cd /d %~dp0

::here change menu name
set menuName_Bpm=Bpm

set regRoot=HKEY_CLASSES_ROOT\*\shell\
set "regPath_Bpm=%regRoot%%menuName_Bpm%"

reg add "%regPath_Bpm%\command" /d "%cd%\BPMAnalyzer.exe \"%%1\"" /f
reg add "%regPath_Bpm%" /v AppliesTo /t REG_SZ /d ".wav OR .mp3" /f

pause