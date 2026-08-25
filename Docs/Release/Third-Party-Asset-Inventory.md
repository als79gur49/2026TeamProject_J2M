# Third-Party Asset Inventory

검토 기준일: 2026-08-26 KST
대상: VectorQuake Windows Direct / Steam 배포 후보와 저장소에 포함된 외부 자산

이 문서는 내부 권리·고지 관리용 인벤토리다. 배포물에 포함되는 공개 고지문은
루트의 `ThirdPartyNotices.txt`와 `UnityPlayerThirdPartyNotices.pdf`가 담당한다.
이 문서는 법률 자문이나 구매 증빙을 대신하지 않는다.

`ThirdPartyNotices.txt`는 두 상위 분류로 구성한다. `PART I`은 배포 시 필요한 라이선스,
저작권 및 플랫폼 조건부 법적 고지를 모으고, `PART II`는 고지 의무 유무와 별개로 현재
식별·취득 사실을 확인한 상용 third-party 에셋을 투명성 목적으로 공개한다. `PART II`는
공급자의 소유권을 프로젝트가 취득했다는 뜻이 아니며, 아래 provenance 보류 항목까지
전수 확인되었다는 완전성 주장도 아니다.

법적 라이선스 취득 주체는 Git 저장소나 프로젝트 자체가 아니라 구매 계정에 연결된
개인 또는 법인이다. 공개 고지의 “취득”은 그 주체가 적용 약관에 따라 에셋을 제품에
사용·통합하고 제품의 일부로 배포할 권리를 확보했다는 뜻이다. 원본 에셋의 저작권이나
소유권이 이전됐다는 뜻이 아니며, 공개 고지만으로 제품 수령자에게 원본 에셋을 별도로
재사용·재배포할 권리가 이전되거나 재허여되지 않는다.

이번 초안은 Git tracked file, Unity `.meta`의 Asset Store 표식, package/local license
원문, `ThirdPartyNotices.txt`, build scene 참조를 대조해 작성했다. 출처 metadata가 없는
binary creative asset은 파일 내용만으로 권리자를 확정할 수 없으므로, 팀의 원본
manifest와 구매 기록을 대조하기 전까지 완전한 전수조사로 간주하지 않는다.

## 판정 기준

- `확인`: 저장소 원문 또는 공식 약관에서 권리와 고지 의무를 확인했고 현재 공개
  고지에도 반영되어 있다.
- `조건부`: 일반 약관상 게임 배포는 가능하지만 구매 계정, 구매 당시 약관, 좌석,
  원본 파일-팩 대응표 중 하나 이상을 내부 증빙으로 확인해야 한다.
- `보류`: 배포 후보에서 발견됐지만 권리 증빙 또는 공개 고지가 빠져 있어 확인 전
  출시 판단에 사용할 수 없다.
- `해당 없음`: Direct 또는 Steam 중 특정 배포 대상에만 포함되는 항목이다.

`상업적 사용 가능`은 완성 게임에 임베드하여 배포하는 경우만 뜻한다. 원본 에셋,
소스 파일, 샘플, 음원을 추출 가능한 형태로 재판매·재배포할 권리를 뜻하지 않는다.

## 우선 결론

1. `AllSky - 220+ Sky / Skybox Set`의 `Space_Nebula_BlueRed` tracked file 25개가 저장소에
   있고, 해당 skybox material은 빌드 설정에 포함된 `UIAudioScene`에서 참조된다.
   원본 GUID도 AllSky 패키지 인벤토리와 일치한다. 2026-08-26 KST에 Unity Asset Store
   구매 사실을 확인했고 `ThirdPartyNotices.txt`의 상용 에셋 목록에 반영했다.
2. 상용 에셋과 Humble Bundle 에셋은 공개 출처와 일반 약관은 정리돼 있지만,
   저장소에는 구매 영수증, 구매 계정/법인, 구매 당시 EULA, 좌석 수가 없다.
   공개 Git 저장소에 영수증을 추가하지 말고 별도 비공개 증빙 저장소에서 관리한다.
3. Ovani Sound 4개 팩은 팩 이름까지는 기록돼 있으나 각 실제 음원 파일이 어느 팩에서
   왔는지 추적하는 manifest가 없다. 파일 단위 provenance를 보강해야 한다.
4. Unity Asset Store 에셋의 설치 버전이 저장소에 체계적으로 기록돼 있지 않다.
   현재 상품 페이지의 최신 버전을 설치 버전으로 간주하면 안 된다.
5. `UnityPlayerThirdPartyNotices.pdf` 원문에는 UPM 패키지명과 Unity Companion License가
   없다. Player PDF는 Unity Player 내장 구성요소를, `ThirdPartyNotices.txt`는 프로젝트가
   추가한 runtime UPM을 각각 담당한다. UCL runtime 패키지 저작권과 UCL v1.4 전문을
   공개 고지에 별도로 반영했다.

## 상용 에셋 및 음원 Matrix

| ID | 에셋 / 공급자 | 저장소 범위 | 취득 경로·라이선스 | 상업적 사용 | 크레딧 의무 | 라이선스 표기 의무 | 주요 제한 | 공개 고지 | 상태 |
|---|---|---|---|---|---|---|---|---|---|
| C-01 | AllSky - 220+ Sky / Skybox Set / rpgwhitelock | `Assets/3DM/SideWall/Space_Nebula_BlueRed/` (25 tracked files), `UIAudioScene` 참조 | Unity Asset Store 구매 확인; [상품](https://assetstore.unity.com/packages/2d/textures-materials/sky/allsky-220-sky-skybox-set-10109), Standard Unity Asset Store EULA로 표시 | 조건 충족 시 가능 | 별도 요구 확인 안 됨 | EULA 전문 동봉 요구는 확인 안 됨 | 완성 제품에 embedded component로만 배포; 구매 주체·license tier와 비공개 Evidence ID 확인 필요 | 반영됨 | 조건부 |
| C-02 | DOTween Pro / Demigiant | `Assets/Plugins/Demigiant/` | [Unity Asset Store 상품](https://assetstore.unity.com/packages/tools/visual-scripting/dotween-pro-32416), [공급자 라이선스](https://dotween.demigiant.com/license.php) | 가능 | 앱 크레딧 요구 확인 안 됨 | Unity plugin 재배포 시 copyright와 원본 readme 요구; 완성 앱에는 별도 요구 확인 안 됨 | 프로젝트 작업자마다 유효한 Pro 라이선스 필요; 설치 버전 확인 필요 | 반영됨 | 조건부 |
| C-03 | All In 1 Sprite Shader / Seaside Studios | `Assets/Plugins/AllIn1SpriteShader/` | [Unity Asset Store 상품](https://assetstore.unity.com/packages/vfx/shaders/all-in-1-sprite-shader-156513), Standard Unity Asset Store EULA | 조건 충족 시 가능 | 별도 요구 확인 안 됨 | EULA 전문 동봉 요구는 확인 안 됨 | 상품 페이지가 Extension Asset으로 표시됨; 필요한 seat 수 확인 | 반영됨 | 조건부 |
| C-04 | Polygon Arsenal / Archanor VFX | `Assets/Polygon Arsenal/` 및 파생 VFX prefab | [Unity Asset Store 상품](https://assetstore.unity.com/packages/vfx/particles/polygon-arsenal-109286), Standard Unity Asset Store EULA | 조건 충족 시 가능 | 별도 요구 확인 안 됨 | EULA 전문 동봉 요구는 확인 안 됨 | 원본/추출 가능 자산 재배포 금지; 상품 페이지의 license tier와 seat 기준 확인 | 반영됨 | 조건부 |
| C-05 | Toon Shaders Pro for URP / Daniel Ilett | `Assets/Toon Shaders Pro/` | [Unity Asset Store 상품](https://marketplace.unity.com/packages/vfx/shaders/toon-shaders-pro-for-urp-305845), Standard Unity Asset Store EULA | 조건 충족 시 가능 | 별도 요구 확인 안 됨 | EULA 전문 동봉 요구는 확인 안 됨 | 상품 페이지가 Extension Asset으로 표시됨; 필요한 seat 수 확인 | 반영됨 | 조건부 |
| C-06 | INTERFACE - Sci-Fi Soldier HUD / Synty Studios | `Assets/Synty/InterfaceCore/`, `Assets/Synty/InterfaceSciFiSoldierHUD/` | Humble Bundle; [Synty One-Time Purchase Licence](https://syntystore.com/pages/one-time-purchase-licence) | 조건 충족 시 가능 | 별도 요구 확인 안 됨 | EULA 전문 동봉 요구는 확인 안 됨; 포함 OFL 폰트는 별도 F 항목 적용 | Humble Bundle은 1 seat; source files 외부 공유 금지; 구매 당시 약관 확인 | 반영됨 | 조건부 |
| C-07 | POLYGON - Particle FX Pack / Synty Studios | `Assets/PolygonParticleFX/` 및 파생 VFX prefab | Humble Bundle; [Synty One-Time Purchase Licence](https://syntystore.com/pages/one-time-purchase-licence) | 조건 충족 시 가능 | 별도 요구 확인 안 됨 | EULA 전문 동봉 요구는 확인 안 됨 | Humble Bundle은 1 seat; source files 외부 공유 금지; 구매 당시 약관 확인 | 반영됨 | 조건부 |
| A-01 | Casual & Mobile Sound FX Pack Vol. 3 / Ovani Sound | `Assets/_Shared/Audio/Clips/` 내 UI/SFX 후보; 파일별 대응표 없음 | Humble Bundle - Audio Apocalypse; [상품](https://ovanisound.com/products/casual-mobile-sound-fx-pack-vol-3), [약관](https://ovanisound.com/policies/terms-of-service) | 게임에 통합 시 가능 | Sound FX/Music은 불필요 | 약관 전문 동봉 요구 확인 안 됨 | 원본 음원 단독 배포·판매·sublicense 금지; 구매 당시 약관과 파일 manifest 확인 | 반영됨 | 조건부 |
| A-02 | Horror Music Pack Vol. 2 / Ovani Sound | `Assets/_Shared/Audio/Clips/Bgm/Horror Vol2 Factory Main.wav` | Humble Bundle - Audio Apocalypse; [상품](https://ovanisound.com/products/horror-music-pack-vol-2), [약관](https://ovanisound.com/policies/terms-of-service) | 게임에 통합 시 가능 | Sound FX/Music은 불필요 | 약관 전문 동봉 요구 확인 안 됨 | 원본 음원 단독 배포 금지; Content ID 등 타 이용자를 방해하는 독점 등록 금지 | 반영됨 | 조건부 |
| A-03 | Superheroes Sound FX Pack Vol. 2 / Ovani Sound | `Assets/_Shared/Audio/Clips/` 내 gameplay SFX 후보; 파일별 대응표 없음 | Humble Bundle - Audio Apocalypse; [상품](https://ovanisound.com/products/superheroes-sound-fx-pack-vol-2), [약관](https://ovanisound.com/policies/terms-of-service) | 게임에 통합 시 가능 | Sound FX/Music은 불필요 | 약관 전문 동봉 요구 확인 안 됨 | 원본 음원 단독 배포·판매·sublicense 금지; 구매 당시 약관과 파일 manifest 확인 | 반영됨 | 조건부 |
| A-04 | Mutated Beings Sound FX Pack / Ovani Sound | `Assets/_Shared/Audio/Clips/Sfx/Monster*` 후보; 파일별 대응표 없음 | Humble Bundle - Audio Alchemy; [상품](https://ovanisound.com/products/mutated-beings-sound-fx-pack-1), [약관](https://ovanisound.com/policies/terms-of-service) | 게임에 통합 시 가능 | Sound FX/Music은 불필요 | 약관 전문 동봉 요구 확인 안 됨 | 원본 음원 단독 배포·판매·sublicense 금지; 구매 당시 약관과 파일 manifest 확인 | 반영됨 | 조건부 |

Unity Asset Store의 현재 표준 EULA는 non-restricted asset을 상당한 독자 콘텐츠가 있는
완성 제품에 embedded component로 넣고 배포·수익화하는 것을 허용한다. 별도 Provider
EULA 또는 Restricted Asset Terms가 있으면 그 조건이 우선한다. Extension Asset은
per-seat 조건이 적용될 수 있으므로 상품명만으로 좌석 충족을 추정하지 않는다.

Synty의 현재 페이지는 Humble Bundle 자산의 상업 게임 사용을 허용하지만 1 seat로
제한한다. 또한 페이지 자체가 과거 구매 권리는 당시 라이선스에 따른다고 설명하므로,
현재 웹페이지 캡처만으로 2026년 이전 취득 권리를 확정하지 않는다.

## 오픈소스·플랫폼·런타임 Matrix

| ID | 구성요소 | 저장소 / 배포 범위 | 라이선스 | 상업적 사용 | 크레딧 의무 | 라이선스 표기 의무 | 배포 대상 | 상태 |
|---|---|---|---|---|---|---|---|---|
| O-01 | Unity UI Extensions | `Assets/Unity UI Extensions/`, `Assets/Synty/InterfaceCore/Scripts/UnityUIExtensions/` | BSD 3-Clause | 가능 | 별도 엔드크레딧은 불필요 | binary 배포 시 copyright, 조건, disclaimer를 문서/기타 자료에 재현 | Direct + Steam | 확인 |
| O-02 | Steamworks.NET 2025.165.0-j2m.1 | `Packages/com.j2m.thirdparty.steamworksnet/` | MIT | 가능 | 별도 엔드크레딧은 불필요 | copyright와 permission notice를 copies/substantial portions에 포함 | Steam만 | 확인 |
| P-01 | Valve Steamworks SDK redistributable (`steam_api64.dll`) | Steam payload | 비공개 Steamworks SDK partner agreement | Steam 승인 파트너·해당 계약 범위에서만 가능 | 계약 확인 필요 | 공개 MIT 고지로 대체 불가 | Steam만 | 조건부 |
| U-01 | Unity Player 6000.3.11f1 / Windows / Mono | Windows Player | Unity 라이선스 및 Player bundled third-party licenses | 유효한 Unity 사용권과 각 조건 범위에서 가능 | PDF 원문 기준 | `UnityPlayerThirdPartyNotices.pdf`를 무변경 배포 | Direct + Steam | 확인 |
| U-02 | `com.unity.cinemachine` 3.1.6 | runtime UPM | Unity Companion License; bundled Clipper/Boost | 가능 | bundled notice 기준 | 공개 고지에 package notice 유지 | Direct + Steam | 확인 |
| U-03 | `com.unity.nuget.newtonsoft-json` 3.2.2 | runtime UPM | Unity Companion License; bundled MIT components | 가능 | 별도 엔드크레딧은 불필요 | bundled MIT notices 유지 | Direct + Steam | 확인 |
| U-04 | `com.unity.localization` 1.5.12 | runtime UPM | Unity Companion License; bundled SmartFormat notice | 가능 | bundled notice 기준 | 공개 고지에 package notice 유지 | Direct + Steam | 확인 |
| U-05 | `com.unity.visualscripting` 1.9.10 | runtime UPM | Unity Package Distribution License; bundled OSS | 가능 | bundled notice 기준 | 공개 고지에 runtime package notices 유지 | Direct + Steam | 확인 |
| U-06 | `com.unity.render-pipelines.universal` 17.3.0 | runtime UPM | Unity Companion License; bundled FXAA notice | 가능 | bundled notice 기준 | 공개 고지에 package notice 유지 | Direct + Steam | 확인 |
| U-07 | `com.unity.render-pipelines.core` 17.3.0 | runtime UPM | Unity Companion License; bundled third-party notices | 가능 | bundled notice 기준 | 공개 고지에 package notice 유지 | Direct + Steam | 확인 |
| U-08 | `com.unity.mathematics` 1.3.3 | runtime UPM | Unity Companion License; bundled Noise MIT | 가능 | 별도 엔드크레딧은 불필요 | bundled MIT notice 유지 | Direct + Steam | 확인 |
| U-09 | `com.unity.burst` 1.8.28 | runtime UPM | Unity Companion / Package Distribution License; bundled OSS | 가능 | bundled notice 기준 | LLVM, mimalloc 등 package notices 유지 | Direct + Steam | 확인 |
| U-10 | `com.unity.collections` 2.6.2 | runtime UPM; 과거 Windows `ManagedStripped` 확인 | Unity Companion License | 가능 | 불필요 | 패키지 저작권과 UCL 제공 | Direct + Steam | 확인 |
| U-11 | `com.unity.addressables` 2.9.1 | Localization 의존 runtime UPM; 과거 Windows `ManagedStripped` 확인 | Unity Companion License | 가능 | 불필요 | 패키지 저작권과 UCL 제공 | Direct + Steam | 확인 |
| U-12 | `com.unity.ai.navigation` 2.0.11 | direct runtime UPM; 과거 Windows `ManagedStripped` 확인 | Unity Companion License | 가능 | 불필요 | 패키지 저작권과 UCL 제공 | Direct + Steam | 확인 |
| U-13 | `com.unity.inputsystem` 1.19.0 | runtime asmdef 참조; 과거 Windows `ManagedStripped` 확인 | Unity Companion License | 가능 | 불필요 | 패키지 저작권과 UCL 제공 | Direct + Steam | 확인 |
| U-14 | `com.unity.shadergraph` 17.3.0 | runtime shader library; 과거 Windows `ManagedStripped` 확인 | Unity Companion License | 가능 | 불필요 | 패키지 저작권과 UCL 제공 | Direct + Steam | 확인 |
| U-15 | `com.unity.splines` 2.8.3 | runtime asmdef 참조; 과거 Windows `ManagedStripped` 확인 | Unity Companion License | 가능 | 불필요 | 패키지 저작권과 UCL 제공 | Direct + Steam | 확인 |
| U-16 | `com.unity.timeline` 1.8.11 | direct runtime UPM; 과거 Windows `ManagedStripped` 확인 | Unity Companion License | 가능 | 불필요 | 패키지 저작권과 UCL 제공 | Direct + Steam | 확인 |
| U-17 | `com.unity.ugui` 2.0.0 | runtime UI; 과거 Windows `UnityEngine.UI.dll` 확인 | Unity Companion License | 가능 | 불필요 | 패키지 저작권과 UCL 제공 | Direct + Steam | 확인 |
| U-18 | `com.unity.render-pipelines.universal-config` 17.0.3 | URP runtime dependency; 과거 Windows runtime assembly 확인 | Unity Companion License | 가능 | 불필요 | 패키지 저작권과 UCL 제공 | Direct + Steam | 확인 |
| U-19 | `com.unity.profiling.core` 1.0.3 | Addressables transitive; 과거 Windows `ManagedStripped` 확인 | Unity Companion License | 가능 | 불필요 | 패키지 저작권과 UCL 제공 | Direct + Steam | 조건부 |
| U-20 | `com.unity.scriptablebuildpipeline` 2.6.1 | Addressables transitive; 과거 Windows `ManagedStripped` 확인 | Unity Companion License | 가능 | 불필요 | 패키지 저작권과 UCL 제공 | Direct + Steam | 조건부 |

UPM 버전은 `Packages/packages-lock.json`과 공개 고지가 일치해야 한다. 패키지 갱신 시
버전뿐 아니라 해당 버전의 `Third Party Notices.md`와 runtime 포함 범위를 다시 검토한다.

`U-10`~`U-20`의 설치 패키지에는 별도의 `Third Party Notices.md`가 없었다. 따라서
패키지 저작권과 UCL을 제공하며, 게임 화면이나 엔드크레딧 표시는 요구되지 않는다.
`U-19`와 `U-20`은 과거 Windows 산출물에는 있었지만 현재 exact-revision 정식 빌드가
없으므로 payload 포함 판단은 조건부다. 공개 고지에는 누락 위험을 피하기 위해 포함한다.

## Editor / Build / Test Package Matrix

아래 패키지는 내부 전수 인벤토리에는 포함하지만, production Player에 해당 패키지
파일이 없으면 공개 배포 고지 대상이 아니다. 소스 프로젝트나 개발 도구를 별도로
재배포할 경우에는 각 패키지의 LICENSE와 bundled notice를 다시 적용한다.

| ID | 구성요소 | 주 용도 / Player 판단 | 라이선스 및 bundled notice | 게임 크레딧 | 공개 고지 | 상태 |
|---|---|---|---|---|---|---|
| E-01 | `com.unity.2d.sprite` 1.0.0 | Editor asmdef만 확인 | Unity Package Distribution License; 별도 bundled notice 없음 | 불필요 | Player 미포함 시 불필요 | 확인 |
| E-02 | `com.unity.collab-proxy` 2.11.4 | Unity Version Control Editor | UPDL; MIT, Zlib, BSD, Apache bundled notice | 불필요 | Player 미포함 시 불필요 | 확인 |
| E-03 | `com.unity.ext.nunit` 2.0.5 | Test Framework dependency | UPDL; NUnit MIT bundled notice | 불필요 | Player 미포함 시 불필요 | 확인 |
| E-04 | `com.unity.ide.rider` 3.0.39 | Rider Editor integration | MIT | 불필요 | Player 미포함 시 불필요 | 확인 |
| E-05 | `com.unity.ide.visualstudio` 2.0.26 | Visual Studio Editor integration | MIT; VSWhere MIT, EnvDTE 0BSD bundled notice | 불필요 | Player 미포함 시 불필요 | 확인 |
| E-06 | `com.unity.multiplayer.center` 1.0.1 | Editor 중심; 과거 Player에 Common assembly 흔적 | UPDL; package notice상 제3자 구성요소 없음 | 불필요 | exact-revision Player 확인 필요 | 조건부 |
| E-07 | `com.unity.nuget.mono-cecil` 1.11.6 | codegen/test transitive | UCL; Mono.Cecil MIT bundled notice | 불필요 | Player 미포함 시 불필요 | 확인 |
| E-08 | `com.unity.performance.profile-analyzer` 1.3.4 | Editor profiling tool | UCL; 별도 bundled notice 없음 | 불필요 | Player 미포함 시 불필요 | 확인 |
| E-09 | `com.unity.searcher` 4.9.4 | Shader Graph Editor dependency | Unity Companion Package License v1.0 | 불필요 | Player 미포함 시 불필요 | 확인 |
| E-10 | `com.unity.settings-manager` 2.1.1 | Editor settings dependency | UCL; 별도 bundled notice 없음 | 불필요 | Player 미포함 시 불필요 | 확인 |
| E-11 | `com.unity.test-framework` 1.6.0 | Unity Test Framework | UCL; production auto-reference 없음 | 불필요 | Player 미포함 시 불필요 | 확인 |
| E-12 | `com.unity.test-framework.performance` 3.2.0 | performance test runtime; auto-reference 없음 | UCL; Perfolizer MIT bundled notice | 불필요 | Player 미포함 시 불필요 | 확인 |

## Font Matrix

| ID | 폰트 | 저장소 범위 | 라이선스 | 상업적 사용 | 크레딧 의무 | 라이선스 표기 의무 | Reserved Font Name / 제한 | 상태 |
|---|---|---|---|---|---|---|---|---|
| F-01 | Orbitron ExtraBold | `Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/Orbitron/` | SIL OFL 1.1 | 가능 | 별도 엔드크레딧은 불필요 | font copy와 함께 copyright 및 OFL 제공 | 수정본에 Reserved Font Name `Orbitron` 사용 금지(허가 없는 경우) | 확인 |
| F-02 | Exo 2.0 Regular / SemiBold | `Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/exo-2-0/` | SIL OFL 1.1 | 가능 | 별도 엔드크레딧은 불필요 | font copy와 함께 copyright 및 OFL 제공 | 수정본에 Reserved Font Name `Exo` 사용 금지(허가 없는 경우) | 확인 |
| F-03 | Saira Condensed SemiBold derived SDF | `Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/Font_SciFiSoldier_Bold.asset` | SIL OFL 1.1 | 가능 | 별도 엔드크레딧은 불필요 | embedded/derived font software와 함께 copyright 및 OFL 제공 | 수정본에 Reserved Font Name `Saira` 사용 금지(허가 없는 경우) | 확인 |
| F-04 | Climate Crisis KR 2000 / 2019 | `Assets/_Shared/UI/Fonts/` | SIL OFL 1.1 | 가능 | 별도 엔드크레딧은 불필요 | font copy와 함께 copyright 및 OFL 제공 | 수정본에 Reserved Font Name `Climate Crisis` 사용 금지(허가 없는 경우) | 확인 |
| F-05 | Liberation Sans | `Assets/TextMesh Pro/Fonts/` | SIL OFL 1.1 | 가능 | 별도 엔드크레딧은 불필요 | font copy와 함께 copyright 및 OFL 제공 | `Liberation` 등 명시된 Reserved Font Name 제한 준수 | 확인 |

OFL의 copyright 및 전문은 `ThirdPartyNotices.txt`의 `Open Font Software` 절에 포함되어
있다. 폰트를 수정하거나 family/style을 추가하면 Reserved Font Name과 copyright 문구를
다시 검토한다.

## 비공개 증빙 Ledger

다음 필드는 공개 Git이 아닌 접근 통제된 문서 저장소에서 ID만 연결해 관리한다.

| 필드 | 필수 내용 |
|---|---|
| Evidence ID | 예: `TP-C-003`; 저장소 문서에는 비밀 URL이나 영수증 원문을 넣지 않음 |
| 구매 주체 | 개인/법인 이름과 프로젝트 사용 권한 |
| 공급처·주문 번호 | Unity Asset Store, Humble Bundle, Synty, Ovani 등 |
| 취득일 | 적용 약관 버전을 결정할 수 있는 날짜 |
| 구매 당시 약관 | PDF 또는 WARC/HTML snapshot과 SHA-256 |
| 라이선스 tier | Single Entity, Multi Entity, Extension Asset, Humble 1-seat 등 |
| 필요/보유 seat | 개발 참여자 최고 인원과 라이선스 보유 수량 |
| 설치 버전 | 실제 저장소 반입 버전; 현재 상품 페이지 최신 버전과 구분 |
| 원본 manifest | 공급 팩 경로/파일명/해시와 프로젝트 내 경로/파일명/해시 대응 |
| 검토자·검토일 | 판단 책임자와 다음 재검토일 |

## 출시 전 조치

1. **AllSky 잔여 증빙 조건**
   - 확인된 Unity Asset Store 구매 사실을 비공개 Evidence ID와 연결한다.
   - 구매 주체와 license tier를 확인한다.
   - 취득 당시 적용 EULA와 별도 Provider/Restricted Asset Terms 존재 여부를 보존한다.
   - 실제 Windows release lane에서 두 공개 고지가 payload에 포함되는지 재검증한다.
2. C-02~C-07의 구매·좌석 증빙을 비공개 ledger와 연결한다. 특히 Extension Asset과
   Humble 1-seat 조건은 현재 팀 참여 인원과 대조한다.
3. A-01~A-04 원본 팩 manifest를 확보해 각 audio clip의 공급 팩, 원본명, 원본 hash,
   프로젝트 내 rename/transcode 이력을 연결한다.
4. 상용 에셋의 실제 설치 버전을 기록한다. 버전을 알 수 없으면 `unknown`을 유지하고
   상품 페이지 최신 버전을 복사해 채우지 않는다.
5. release candidate마다 EditorBuildSettings, Addressables/Resources, native/managed
   plugin inventory를 기준으로 이 문서와 공개 고지의 포함 범위를 다시 대조한다.

## 근거 위치

- 공개 고지: `ThirdPartyNotices.txt`
- Unity Player 고지: `UnityPlayerThirdPartyNotices.pdf`
- 빌드/고지 검증 계약: `Docs/Testing/Windows-Release-Build-Pipeline.md`
- UPM 고정 버전: `Packages/packages-lock.json`
- Steamworks.NET 원문: `Packages/com.j2m.thirdparty.steamworksnet/LICENSE.md`
- Unity UI Extensions 원문: `Assets/Unity UI Extensions/LICENSE.md`
- 폰트 원문: 각 font directory의 `OFL.txt` 또는 `*-OFL.txt`
- Unity Asset Store EULA: <https://unity.com/legal/as-terms>

## 변경 규칙

- 새 외부 에셋을 반입하는 커밋은 이 inventory row, 비공개 Evidence ID, 원본
  manifest를 함께 준비한다.
- `상업적 사용 가능`과 `크레딧 불필요`는 서로 다른 판단이다.
- `크레딧 불필요`와 `copyright/license notice 동봉 불필요`도 서로 다른 판단이다.
- 공개 고지를 변경할 때는 고정된 section/order/body hash 계약 때문에 텍스트만 단독
  수정하지 말고 빌드 policy와 테스트 fixture를 함께 갱신한다.
- 삭제된 에셋은 row를 지우지 않고 `retired` 상태와 마지막 포함 revision을 기록한다.
