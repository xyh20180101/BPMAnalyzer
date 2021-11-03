cd /d %~dp0

::here change menu name
set menuName_Bpm=Bpm

set regRoot=HKEY_CLASSES_ROOT\*\shell\
set "regPath_Bpm=%regRoot%%menuName_Bpm%"

reg delete "%regPath_Bpm%" /f

pause