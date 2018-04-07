# Makefile

DEL=del /f
COPY=copy /y
CSC=csc /nologo

LIBS=
FLAGS=/target:winexe
#FLAGS=/target:exe
TARGET=winposfixer.exe

all: $(TARGET)

test: $(TARGET)
	.\$(TARGET)

clean:
	-$(DEL) $(TARGET)
	-$(DEL) *.lib *.obj *.res *.exe *.pdb

$(TARGET): WinPosFixer.cs
	$(CSC) $(FLAGS) /out:$@ $** $(LIBS)

WinPosFixer.res: WinPosFixer.ico

.rc.res:
	$(RC) $(RCFLAGS) $<
