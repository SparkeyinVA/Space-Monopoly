# ---------------------------------------------------------------------------
VERSION = BCB.03
# ---------------------------------------------------------------------------
!ifndef BCB
BCB = $(MAKEDIR)\..
!endif
# ---------------------------------------------------------------------------
PROJECT = SpacePoly2.exe
OBJFILES = SpacePoly2.obj SMMain.obj Players.obj ComForm.obj ComInfoForm.obj Merger.obj \
  SplitForm.obj OverForm.obj AboutForm.obj CommaIt.obj playerDetails.obj \
  HighScores.obj crypt.obj Preferences.obj CurrencyUnit.obj Prefs.obj Grid.obj \
  GameGrid.obj Game.obj GridManager.obj large.obj
RESFILES = SpacePoly2.res
RESDEPEN = $(RESFILES) SMMain.dfm Players.dfm ComForm.dfm ComInfoForm.dfm Merger.dfm \
  SplitForm.dfm OverForm.dfm AboutForm.dfm playerDetails.dfm HighScores.dfm \
  Preferences.dfm GridManager.dfm
LIBFILES =
LIBRARIES = vcldbx35.lib vcldb35.lib vclx35.lib vcl35.lib
SPARELIBS = vcl35.lib vclx35.lib vcldb35.lib vcldbx35.lib
PACKAGES = VCLX35.bpi VCL35.bpi VCLDB35.bpi VCLDBX35.bpi bcbsmp35.bpi dclocx35.bpi \
  QRPT35.bpi
PATHASM = .;
PATHCPP = .;
PATHPAS = .;
PATHRC = .;
DEBUGLIBPATH = $(BCB)\lib\debug
RELEASELIBPATH = $(BCB)\lib\release
DEFFILE =
# ---------------------------------------------------------------------------
CFLAG1 = -Od -Hc -w-par -w-inl -Ve -r- -d -k -y -v -vi- -c -b- -Vx
CFLAG2 = -I"d:\program files\borland\cbuilder3\projects";..\..\borland\cbuilder3\bin;..\..\projects;$(BCB)\bin;$(BCB)\include;$(BCB)\include\vcl \
  -H=$(BCB)\lib\vcld.csm
CFLAG3 = -w-eas -f-
PFLAGS = -AWinTypes=Windows;WinProcs=Windows;DbiTypes=BDE;DbiProcs=BDE;DbiErrs=BDE \
  -U"d:\program files\borland\cbuilder3\projects";..\..\borland\cbuilder3\bin;..\..\projects;$(BCB)\bin;$(BCB)\lib\obj;$(BCB)\lib;$(RELEASELIBPATH) \
  -I"d:\program files\borland\cbuilder3\projects";..\..\borland\cbuilder3\bin;..\..\projects;$(BCB)\bin;$(BCB)\include;$(BCB)\include\vcl \
  -$Y -$W -$O- -$C- -v -JPHNV -M
RFLAGS = -i"d:\program files\borland\cbuilder3\projects";..\..\borland\cbuilder3\bin;..\..\projects;$(BCB)\bin;$(BCB)\include;$(BCB)\include\vcl
AFLAGS = /i"d:\program files\borland\cbuilder3\projects" /i..\..\borland\cbuilder3\bin \
  /i..\..\projects /i$(BCB)\bin /i$(BCB)\include /i$(BCB)\include\vcl /mx /w2 /zd
LFLAGS = -L"d:\program files\borland\cbuilder3\projects";..\..\borland\cbuilder3\bin;..\..\projects;$(BCB)\bin;$(BCB)\lib\obj;$(BCB)\lib;$(RELEASELIBPATH) \
  -aa -Tpe -x -v
IFLAGS =
LINKER = ilink32
# ---------------------------------------------------------------------------
ALLOBJ = c0w32.obj sysinit.obj $(OBJFILES)
ALLRES = $(RESFILES)
ALLLIB = $(LIBFILES) $(LIBRARIES) import32.lib cp32mt.lib
# ---------------------------------------------------------------------------
.autodepend

!ifdef IDEOPTIONS

[Version Info]
IncludeVerInfo=1
AutoIncBuild=0
MajorVer=2
MinorVer=0
Release=0
Build=0
Debug=0
PreRelease=1
Special=0
Private=1
DLL=0
Locale=2057
CodePage=1252

[Version Info Keys]
CompanyName=David Vivash
FileDescription=The freeware game of buying and selling shares.
FileVersion=2.0.0.0
InternalName=SpacePoly2
LegalCopyright=2002, David Vivash
LegalTrademarks=n/a
OriginalFilename=SpacePoly2
ProductName=Space Monopoly
ProductVersion=2.0
Comments=Freeware

[HistoryLists\hlIncludePath]
Count=3
Item0=d:\program files\borland\cbuilder3\projects;..\..\borland\cbuilder3\bin;..\..\projects;$(BCB)\bin;$(BCB)\include;$(BCB)\include\vcl
Item1=..\..\borland\cbuilder3\bin;..\..\projects;$(BCB)\bin;$(BCB)\include;$(BCB)\include\vcl
Item2=..\..\projects;$(BCB)\bin;$(BCB)\include;$(BCB)\include\vcl

[HistoryLists\hlLibraryPath]
Count=3
Item0=d:\program files\borland\cbuilder3\projects;..\..\borland\cbuilder3\bin;..\..\projects;$(BCB)\bin;$(BCB)\lib\obj;$(BCB)\lib
Item1=..\..\borland\cbuilder3\bin;..\..\projects;$(BCB)\bin;$(BCB)\lib\obj;$(BCB)\lib
Item2=..\..\projects;$(BCB)\bin;$(BCB)\lib\obj;$(BCB)\lib

[HistoryLists\hlUnitAliases]
Count=1
Item0=WinTypes=Windows;WinProcs=Windows;DbiTypes=BDE;DbiProcs=BDE;DbiErrs=BDE

[Debugging]
DebugSourceDirs=

[Parameters]
RunParams=
HostApplication=

!endif

$(PROJECT): $(OBJFILES) $(RESDEPEN) $(DEFFILE)
    $(BCB)\BIN\$(LINKER) @&&!
    $(LFLAGS) +
    $(ALLOBJ), +
    $(PROJECT),, +
    $(ALLLIB), +
    $(DEFFILE), +
    $(ALLRES) 
!

.pas.hpp:
    $(BCB)\BIN\dcc32 $(PFLAGS) { $** }

.pas.obj:
    $(BCB)\BIN\dcc32 $(PFLAGS) { $** }

.cpp.obj:
    $(BCB)\BIN\bcc32 $(CFLAG1) $(CFLAG2) -o$* $* 

.c.obj:
    $(BCB)\BIN\bcc32 $(CFLAG1) $(CFLAG2) -o$* $**

.rc.res:
    $(BCB)\BIN\brcc32 $(RFLAGS) $<
#-----------------------------------------------------------------------------
