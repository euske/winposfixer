# Makefile

DEL=del /f
COPY=copy /y
CSC=csc /nologo

LIBS=
FLAGS=/target:winexe
FLAGS_DEBUG=/target:exe
TARGET=winposfixer.exe
TARGET_DEBUG=winposfixer_d.exe
DESTDIR=%UserProfile%\bin
SRCS=WinPosFixer.cs WinPosActive.ico WinPosInactive.ico
WIN32ICON=/win32icon:WinPosActive.ico
RESOURCES=/res:WinPosActive.ico /res:WinPosInactive.ico

all: $(TARGET)

install: $(TARGET)
	$(COPY) $(TARGET) $(DESTDIR)

test: $(TARGET_DEBUG)
	.\$(TARGET_DEBUG)

clean:
	-$(DEL) $(TARGET)
	-$(DEL) *.lib *.obj *.res *.exe *.pdb

$(TARGET): $(SRCS)
	$(CSC) $(FLAGS) $(WIN32ICON) /out:$@ WinPosFixer.cs $(RESOURCES) $(LIBS)

$(TARGET_DEBUG): $(SRCS)
	$(CSC) $(FLAGS_DEBUG) /out:$@ WinPosFixer.cs $(RESOURCES) $(LIBS)
