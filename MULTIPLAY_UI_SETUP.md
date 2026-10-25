# 직접 만든 UI 연결 방법

씬 파일과 기존 오브젝트는 이번 작업에서 수정하지 않았다. 아래 설정을 Unity Inspector에서 직접 연결한다. Play를 끈 상태에서 작업한다.

## 1. PadBoard 두 개

A/B 각각의 기존 PadBoard Panel에 MultiplayPadBoard 컴포넌트를 추가하고, HUD의 Pad Board 칸에 연결한다. 실행하면 Panel 내부에 패드 64개가 자동 생성된다. 기존 Panel과 자식 오브젝트는 유지된다. Padding과 Spacing으로 안쪽 여백과 간격을 조절한다.

## 2. MultiplayHud

원하는 관리용 오브젝트에 `MultiplayHud`를 추가한다. 같은 오브젝트에 컨트롤러를 추가해도 된다.

Player A / Player B를 각각 펼쳐 다음을 연결한다.

| Inspector 필드 | 넣을 항목 |
|---|---|
| Pad Board | 해당 플레이어 Panel에 붙인 MultiplayPadBoard 컴포넌트 |
| Nickname Text | NicknameText |
| Score Text | ScoreText |
| Combo Text | ComboText |
| Device Status Text | DeviceStatusText |
| Judgment Text | JudgmentText |

공통 필드:

| Inspector 필드 | 넣을 항목 |
|---|---|
| Score Difference Text | 가운데 ScoreDifferenceText |
| Lead Slider | LeadSlider의 Slider 컴포넌트 |
| Lead Fill | LeadSlider / Fill Area / Fill의 Image |
| Lead Handle | LeadSlider / Handle Slide Area / Handle의 Image (선택) |
| Instruction Text | 카운트다운·준비 안내용 TMP 텍스트 (선택) |
| Result Text | 승패 표시용 TMP 텍스트 (선택) |

LeadSlider 자체의 Direction을 **Left To Right**로 설정한다. A 우세면 왼쪽/파랑, B 우세면 오른쪽/빨강, 동점이면 중앙/흰색이다. 숫자는 `+100`처럼 차이만 표시하며 동점은 `0`이다. Fill/Handle을 연결해야 해당 이미지의 색도 변경된다.

`Lead Full Scale Points` 기본값은 10000이다. 10000점 차이에서 끝에 도달하며 100점 차이는 전체 폭의 0.5% 이동한다. 작은 점수 차이도 크게 보이게 하려면 이 값을 낮춘다.

Combo Text는 실행 시 #00FF75 및 가로·세로 중앙 정렬로 설정되고 `COMBO` 다음 줄에 숫자가 나온다. 폰트·크기·위치는 기존 설정을 유지한다. Device Status Text는 흰색이며, 등록한 장치가 끊어지면 빨간색 DISCONNECTED로 바뀐다. 재연결 시 흰색으로 돌아오지만 취소된 라운드는 재시작하지 않는다.

## 3. MultiplayRhythmController

관리용 오브젝트에 `MultiplayRhythmController`를 추가하고 다음을 연결한다.

| Inspector 필드 | 넣을 항목 |
|---|---|
| Song Chart | Crystal Of Lasher 등 SongChart 에셋 |
| Hud | 위에서 추가한 MultiplayHud |
| Midi | 씬에서 사용할 LaunchpadMidiInput 컴포넌트 하나 |
| Music | 곡 재생용 AudioSource 컴포넌트 |

LaunchpadMidiInput / AudioSource가 없다면 관리용 오브젝트에 각각 추가한다. AudioSource의 Play On Awake는 끈다. MIDI 큐는 하나의 게임 컨트롤러만 소비해야 하므로 같은 씬에 튜토리얼/싱글 컨트롤러를 동시에 활성화하지 않는다.

두 런치패드에서 각각 그리드 패드를 누르면 준비되고 동시에 시작한다. 재대결 기능은 없다. 종료/연결 해제 후 승패 또는 취소 텍스트를 유지한다. 메뉴 버튼이 필요하다면 직접 만든 버튼의 On Click에 컨트롤러의 `ReturnToMenu()`를 연결할 수 있다.

MultiplayHud의 Countdown Panel과 Result Panel에 기존 Panel을 연결하면 준비 중에는 둘 다 숨기고, 카운트다운 중에는 Countdown만 표시하며, 플레이 시작 시 숨긴다. 종료 또는 연결 해제에 따른 취소 시에는 Result를 표시한다. 텍스트 필드를 비우면 각 Panel 안의 첫 TMP 텍스트를 자동으로 사용한다. 텍스트가 여러 개라면 Instruction Text와 Result Text도 직접 지정한다. HUD와 게임 컨트롤러는 이 두 Panel 내부가 아닌 항상 활성화된 별도 관리용 오브젝트에 둔다. Panel에는 표시할 TMP 텍스트가 있어야 하며 스크립트가 새 텍스트를 생성하지는 않는다.

## 확인

C# 컴파일 통과. MultiPlay 씬 파일 해시가 작업 전후 동일함을 확인했다. Inspector 연결 후 실제 Play 화면과 런치패드 입력은 별도 확인이 필요하다.

승패 표시 시점: 음원 길이가 아니라 마지막 채보 노트(Hold 종료와 늦은 입력 판정 시간 포함)가 끝난 뒤 2초 후 Result Panel을 표시한다. 채보가 끝나면 음악과 입력 판정이 종료되며, 2초 대기 중 점수는 고정된다. 연결 해제로 취소된 경우는 취소 결과를 바로 표시한다.

두 플레이어가 모두 준비하면 3초 동안 3 → 2 → 1을 표시한다. 카운트다운 패널을 숨긴 뒤 2초 더 기다리고 곡과 채보의 공통 시간이 시작된다(준비 완료 기준 총 5초). 대기 마지막 구간에는 기존 설정대로 첫 노트의 미리보기와 이른 입력 판정을 허용한다. Result Text는 A 승리 시 Player A Color(파랑), B 승리 시 Player B Color(빨강), 무승부와 취소 시 흰색으로 표시한다.
