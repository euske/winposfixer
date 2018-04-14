# Makefile

DEL=del /f
COPY=copy /y
CSC=csc /nologo

LIBS=
FLAGS=/target:winexe
FLAGS_DEBUG=/target:exe
TARGET=winposfixer.exe
TARGET_DEBUG=winposfixer_d.exe

all: $(TARGET)

test: $(TARGET_DEBUG)
	.\$(TARGET_DEBUG)

clean:
	-$(DEL) $(TARGET)
	-$(DEL) *.lib *.obj *.res *.exe *.pdb

$(TARGET): WinPosFixer.cs WinPosActive.ico WinPosInactive.ico
	$(CSC) $(FLAGS) /out:$@ WinPosFixer.cs /win32icon:WinPosActive.ico /res:WinPosActive.ico /res:WinPosInactive.ico $(LIBS)

$(TARGET_DEBUG): WinPosFixer.cs WinPosActive.ico WinPosInactive.ico
	$(CSC) $(FLAGS_DEBUG) /out:$@ WinPosFixer.cs /win32icon:WinPosActive.ico /res:WinPosActive.ico /res:WinPosInactive.ico $(LIBS)
