# Chojema TickerBar

주요 일정과 메모를 화면 가장자리에 흐르는 띠로 보여 주는 Windows 앱입니다. 데이터는 PC에만 저장되며 외부로 전송하지 않습니다.

[English](#english)

## 설치

1. [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0)을 설치합니다.
2. [Releases](https://github.com/chojema/chojema-tickerbar/releases/latest)에서 `ChojemaTickerBar.exe`를 내려받아 원하는 폴더에 두고 실행합니다.

서명되지 않은 앱이라 처음 실행할 때 Windows가 경고를 표시할 수 있습니다. **추가 정보**를 누른 뒤 **실행**을 선택합니다.

로그인할 때 자동으로 실행하려면 `Win + R`에 `shell:startup`을 입력해 열린 폴더에 실행 파일의 바로가기를 넣습니다.

## 사용법

- **항목 관리**: 티커를 두 번 클릭하거나 알림 영역 아이콘을 두 번 클릭합니다.
- **일정**: `12-1`, `2026-12-01`, `12-1 14:30`처럼 입력하거나 달력과 시계 버튼으로 고릅니다. 비워 두면 메모가 됩니다.
- **반복**: 매일, 매주, 매월, 매년 중에서 고릅니다. 반복 일정에 **완료**를 체크하면 다음 회차로 넘어갑니다.
- **경고 표시**: 마감 24시간 이내(매일 반복은 1시간 이내)는 **[임박]**, 지나간 반복 일정은 **[지연]**으로 색을 달리해 표시합니다.
- **표시 설정**: 모니터, 위쪽/아래쪽 위치, 흐르는 방향, 날짜 표시, 항상 위 표시를 바꿀 수 있으며 바로 적용됩니다.
- **숨기기와 종료**: 알림 영역 메뉴에서 티커를 숨기거나 종료합니다. 띠 오른쪽 끝의 **×**로도 종료할 수 있습니다.

띠 왼쪽에는 현재 창의 입력 상태(**한**, **A**)가, 오른쪽에는 오늘 날짜가 표시됩니다.

## 데이터 위치

`%LOCALAPPDATA%\Chojema TickerBar\state.json`

앱을 삭제할 때는 실행 파일과 이 폴더를 지우면 됩니다.

## 소스에서 빌드

.NET 10 SDK가 필요합니다.

```powershell
dotnet run --project .\ChojemaTickerBar.csproj
dotnet run --project .\tests\TickerBar.Checks.csproj
dotnet publish .\ChojemaTickerBar.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=None
```

## 버전

[유의적 버전](https://semver.org/lang/ko/)을 따릅니다. `주.부.수`에서 저장 데이터와 호환되지 않는 변경은 주 번호, 기능 추가는 부 번호, 버그 수정은 수 번호를 올립니다. 변경 내역은 [Releases](https://github.com/chojema/chojema-tickerbar/releases)에 있습니다.

## 라이선스

[MIT](LICENSE)

---

## English

Chojema TickerBar is a Windows app that scrolls your key appointments and notes along the edge of the screen. All data stays on your PC.

**Install**: Install the [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0), then download `ChojemaTickerBar.exe` from [Releases](https://github.com/chojema/chojema-tickerbar/releases/latest) and run it. The app is unsigned, so Windows may show a warning on first launch; choose **More info**, then **Run anyway**. To start it at sign-in, put a shortcut in the folder opened by `shell:startup`.

**Use**: Double-click the ticker or the tray icon to manage items. Enter dates as `12-1`, `2026-12-01`, or `12-1 14:30`, or leave the date empty for a note. Items can repeat daily, weekly, monthly, or yearly. Items due within 24 hours (1 hour for daily items) are marked **[임박]** (due soon), and missed recurring items **[지연]** (overdue). The display settings tab controls the monitor, edge, direction, date badge, and always-on-top mode. The interface is in Korean.

**Data**: `%LOCALAPPDATA%\Chojema TickerBar\state.json`

**Versioning**: [Semantic Versioning](https://semver.org/). **License**: [MIT](LICENSE).
