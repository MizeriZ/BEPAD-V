# BEPAD.V 로컬 2인 대전

## 프로젝트 분석

- Unity 6000.4.1f1, Windows WinMM MIDI 입력/LED 출력 사용.
- TitleLaunchpadLobby → ModeSelect 닉네임 → Tutorial / FREESTYLE / MultiPlay 흐름.
- SongChart에 음원, BPM, 오프셋, Tap/Hold 패턴이 들어 있음.
- 기존 FreestyleRhythmController는 1인 전용: 입력 소비, 노트 상태, 점수, 결과 저장 모두 1인 기준.
- 기존 MultiPlay 씬에는 카메라, Canvas, EventSystem만 있었음.
- GameSession에 이미 2인 닉네임과 점수 저장 공간이 있었음.
- 싱글 점수는 노트별 원점수 합산 방식임. 이번 100만점 환산은 멀티에 적용됨.

## 구현

씬은 직접 만든 상태로 유지되며, UI는 Inspector에서 수동 연결합니다. MULTIPLAY_UI_SETUP.md의 설정 방법을 따르세요. 자동 UI 생성 및 재대결 기능은 제거했습니다. MK2/X 장치 구분과 점수 계산은 유지됩니다.

1. 타이틀에서 A가 먼저 참가하고 B가 참가한다. 닉네임과 장치 순서를 대전까지 유지한다.
2. MULTIPLAY 진입 후 각자 그리드 패드를 눌러 준비한다. 두 명이 준비되어야 시작한다.
3. 공통 DSP 시간으로 음원 하나를 예약 재생한다. 음원 시작 전에도 첫 노트의 프리뷰와 이른 판정을 처리한다.
4. 각자 같은 채보를 플레이한다. 노트 상태, Hold 입력/해제, 판정, 콤보, 점수는 독립적이다.
5. 원점수 누계 / 채보 최대 원점수 × 1,000,000으로 점수를 계산한다. 매번 누계를 환산하므로 전부 PERFECT면 정확히 100만점이다.
6. 실시간 선두와 점수 차이를 표시하고, 음원과 마지막 노트가 끝나면 승자 또는 DRAW를 표시한다.
7. 재대결 없이 결과를 유지한다. 직접 만든 메뉴 버튼은 ReturnToMenu를 연결해 ModeSelect로 돌아갈 수 있다.
8. 카운트다운/플레이 중 장치가 사라지면 라운드를 취소한다. 연결 상태 텍스트는 재연결 여부를 계속 표시한다.

판정은 기존 싱글에 맞췄다: PERFECT 60ms, GREAT 120ms, GOOD 220ms, COOL 350ms. Hold는 시작 판정창에 들어온 뒤 해제 시점에 2배 점수를 받는다. 너무 일찍/늦게 해제하면 COOL, 시작하지 못하면 MISS다.

## 하드웨어 제약과 확인

지원 대상으로 **MK2 + MK2**, **MK2 + X**, **X + X**를 처리한다. DeviceKey는 모델명이 아니라 Windows MIDI 장치 인터페이스의 인스턴스 경로를 사용한다. 같은 이름의 MK2 두 대도 별도로 등록하며, MIDI 입력/출력 번호가 서로 다른 순서여도 같은 장치의 LED 출력에 연결한다. 타이틀의 A/B 지정은 튜토리얼 표시와 대전까지 유지된다. USB 포트를 바꾸면 장치 경로가 달라질 수 있으므로 타이틀에서 다시 참가한다. 대전 중 연결이 끊기면 라운드는 취소되며 자동 재대결하지 않는다.

X에서는 DAW 포트를 제외하고 연주용 MIDI 포트를 선택한다. 같은 장치의 여러 포트가 동일 Windows 인터페이스로 보고되면 하나만 사용하며, Windows의 MIDIIN2/MIDIOUT2 포트를 우선한다. **X는 Programmer 모드로 설정해서 테스트한다.** X 한 대로 FREESTYLE 입력/LED를 확인할 수 있으며, MultiPlay 전체 라운드에는 두 장치가 필요하다.

드라이버가 장치 인터페이스 조회를 제공하지 않을 때는 포트별 식별자로 입력을 분리하고 이름이 유일한 출력만 연결한다. 이름도 같은 여러 장치의 출력 관계가 불명확하면 다른 장치에 LED를 보내는 대신 Console에 경고를 남긴다. Console의 `Launchpad input ...; output ...; key ...` 로그로 연결된 입력/출력 조합을 확인할 수 있다. 이 예외에서는 Windows MIDI 드라이버/포트 정보를 확인해야 하며, 다른 MIDI 프로그램이 포트를 점유하고 있다면 닫고 씬을 다시 연다.

LED와 그리드는 기존 코드와 같은 11~88 노트 번호 배열을 사용한다. 실제 기기가 이 배열과 외부 LED 제어를 지원하는 모드인지 확인해야 한다. MIDI 수신 시각 대신 Unity 프레임에서 소비한 시각으로 판정하는 기존 방식이므로 프레임 지연의 영향이 남아 있다.

## 검증

`Tests/MultiplayScoring.Tests.ps1`은 환산, 상한, 경계 판정, 100점 차이 예제를 검증한다.

`Tests/LaunchpadPortRouting.Tests.ps1`은 같은 이름의 MK2 두 대, 뒤바뀐 출력 순서, 출력 중복 할당 금지, 입력 번호 변경, X 별칭 포트 중복 제거, 혼합 조합과 연결 해제를 검증한다. 하드웨어 없는 모의 포트 테스트 16개와 점수 테스트 11개가 통과했다. Unity 프로젝트의 참조와 컴파일 옵션으로 C# 컴파일도 통과했다.

실기 확인 순서:

- Unity 컴파일 완료 후 MultiPlay 씬을 열고 Play.
- MK2 두 대로 검사한 뒤 MK2 한 대를 X로 바꿔 같은 검사를 반복한다. X는 Programmer 모드로 설정한다.
- 두 런치패드에서 각각 준비. 한쪽만 준비했을 때 음악이 시작하지 않는지 확인.
- 동일 노트를 A만 눌러 B의 점수/콤보에 영향이 없는지 확인.
- Tap, Hold, MISS와 좌우 LED를 확인.
- 곡 종료 시 점수와 동점/승패 표시 확인.
- 카운트다운/플레이 중 케이블 분리 → 취소 및 빨간 연결 상태 → 재연결 시 흰색 복귀 확인.
- Title부터 닉네임을 다르게 입력하고 대전에서도 A/B가 유지되는지 확인.

실제 장치 2대를 이용한 입력·LED·화면 검증은 아직 수행하지 않았다.

## 확인한 공식 문서

- [Microsoft: DRV_QUERYDEVICEINTERFACE](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/drv-querydeviceinterface): WinMM MIDI 입력/출력의 Windows 장치 인터페이스 조회.
- [Novation: MIDI on Launchpad X](https://userguides.novationmusic.com/hc/en-gb/articles/24001489325330-MIDI-on-Launchpad-X): DAW 포트와 연주/LED용 MIDI 포트의 구분.
- [Novation: Launchpad X 기본 MIDI 매핑](https://userguides.novationmusic.com/hc/en-gb/articles/23731456402962-Launchpad-X-s-default-MIDI-mappings): Programmer 모드 그리드 매핑.
