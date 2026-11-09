# VR Boundary Demo – Meta Quest 3S (Unity 6.3 LTS)

Projekt demonstracyjny pokazujący **obsługę granic (Boundary / Guardian)** na goglach Meta Quest 3S. Całe sterowanie odbywa się **kontrolerami Touch Plus przez `OVRInput`**:

- **odczyt granicy systemowej** – czy jest skonfigurowana, kształt i rozmiar obszaru gry (`OVRManager.boundary` → `OVRBoundary`),
- **wizualizacja granicy** – linia na podłodze i ściany, które pojawiają się przy zbliżaniu głowy lub kontrolera,
- **testy bliskości** – czy głowa i kontrolery są w środku, blisko krawędzi czy poza granicą; ostrzeżenie przed oczami i **wibracje kontrolera** rosnące przy krawędzi,
- **własna granica aplikacji** rysowana kontrolerem na podłodze (narożnik po narożniku),
- **passthrough i ukrywanie granicy systemowej** (Boundary API, tryb „contextual boundaryless”),
- **układ odniesienia** Floor Level ↔ Stage i reakcja na wyśrodkowanie widoku (recenter).

Scena składa się wyłącznie z **prymitywów Unity**, **paneli UI** oraz **Building Blocks z Meta XR SDK** (rig kamery, kontrolery, passthrough). Nie ma żadnych zewnętrznych modeli ani tekstur.

---

## Spis treści

1. [Wymagania](#1-wymagania)
2. [Zawartość projektu](#2-zawartość-projektu)
3. [Pierwsze otwarcie i konfiguracja](#3-pierwsze-otwarcie-i-konfiguracja)
4. [Build & Run przez USB](#4-build--run-przez-usb)
5. [Instrukcja obsługi w goglach](#5-instrukcja-obsługi-w-goglach)
6. [Jak to działa](#6-jak-to-działa)
7. [Dostrajanie i rozbudowa](#7-dostrajanie-i-rozbudowa)
8. [Rozwiązywanie problemów](#8-rozwiązywanie-problemów)

---

## 1. Wymagania

| Element | Wersja / uwagi |
|---|---|
| Unity | **6000.3.24f1** (Unity 6.3 LTS) z modułem **Android Build Support** (w tym *OpenJDK* oraz *Android SDK & NDK Tools*) – instalacja przez Unity Hub › Installs › Add modules |
| Gogle | Meta Quest 3S z aktualnym Horizon OS, **tryb deweloperski włączony** |
| Kabel | USB-C z transmisją danych (nie tylko do ładowania) |

Pakiety są wpisane w `Packages/manifest.json` i instalują się same przy pierwszym otwarciu projektu (te same wersje co w projekcie *VR Input*):

| Pakiet | Wersja | Rola |
|---|---|---|
| `com.meta.xr.sdk.core` | 207.0.1 | Meta XR Core SDK: `OVRCameraRig`, `OVRManager`, `OVRInput`, `OVRBoundary`, `OVRPassthroughLayer` |
| `com.unity.xr.openxr` | 1.18.0 | Backend OpenXR (zalecany przez Meta) |
| `com.unity.render-pipelines.universal` | 17.3.0 | URP |
| `com.unity.inputsystem` | 1.18.0 | Wymagany przez OpenXR |
| `com.unity.ugui` | 2.0.0 | Panele tekstowe (Canvas + Text) |

Interaction SDK nie jest potrzebny – demo obsługuje wyłącznie kontrolery przez `OVRInput`.

---

## 2. Zawartość projektu

```
unity_vr_granice/
├─ INSTRUKCJA.md                  ← ten plik
├─ Packages/manifest.json         ← lista pakietów
├─ ProjectSettings/ProjectVersion.txt
└─ Assets/VRBoundaryDemo/
   ├─ Editor/
   │  └─ VRBoundaryDemoSetup.cs   ← menu "VR Boundary Demo": konfiguracja, budowa sceny, sprawdzanie bloków, Build & Run
   ├─ Scripts/
   │  ├─ SystemBoundary.cs        ← odczyt granicy systemowej (OVRBoundary) i jej odświeżanie
   │  ├─ BoundaryShape.cs         ← wielokąt granicy: w środku / odległość od krawędzi / pole
   │  ├─ BoundaryVisual.cs        ← linia, słupki i półprzezroczyste ściany granicy
   │  ├─ BoundaryMonitor.cs       ← testy bliskości głowy i kontrolerów, ostrzeżenia, wibracje
   │  ├─ CustomBoundary.cs        ← własna granica rysowana kontrolerem
   │  ├─ PassthroughBoundaryControl.cs ← passthrough + ukrywanie granicy systemowej
   │  ├─ DemoActions.cs           ← mapowanie przycisków OVRInput na akcje
   │  ├─ PlayerLocomotion.cs      ← ruch i obrót gałkami
   │  ├─ Haptics.cs               ← wibracje kontrolerów (poziom ciągły + impulsy)
   │  ├─ BoundaryInfoPanel.cs     ← panel na żywo: stan granic
   │  ├─ DemoRig.cs               ← dostęp do rigu, głowy i kontrolerów
   │  ├─ DemoLog.cs / DemoLogPanel.cs ← dziennik zdarzeń (panel + logcat)
   │  └─ DemoUI.cs, ColorTint.cs, DemoResources.cs ← pomocnicze (prymitywy, panele, kolory)
   ├─ Scenes/BoundaryDemo.unity   ← tworzona przez menu VR Boundary Demo › 2
   ├─ Materials/DemoLit.mat, BoundaryWall.mat ← tworzone automatycznie (URP Lit / przezroczysty Unlit)
   └─ Settings/VRBoundaryDemo_URP*.asset ← tworzone automatycznie (URP)
```

Podział pracy:
- **skrypt edytora** buduje otoczenie, panele i logikę demo,
- **Building Blocks** dostarczają wszystko, co pochodzi z Meta: rig kamery, modele kontrolerów i passthrough.

Skrypty demo same znajdują w scenie `OVRCameraRig` i `OVRPassthroughLayer` z bloków – nic nie trzeba podpinać ręcznie.

---

## 3. Pierwsze otwarcie i konfiguracja

### Krok 0 – otwarcie projektu
1. Unity Hub › **Add › Add project from disk** › wskaż folder `unity_vr_granice`.
2. Otwórz projekt w **6000.3.24f1**. Pierwsze otwarcie trwa kilka minut (pobieranie i import pakietów).
3. Mogą pojawić się okna Meta SDK (np. zgoda na telemetrię, propozycja restartu z nowym systemem wejścia) – zaakceptuj domyślne odpowiedzi. Restart edytora jest normalny.
4. Po imporcie pojawi się okno **VR Boundary Demo** z propozycją konfiguracji – kliknij **Konfiguruj teraz** (albo zrób to z menu, krok 1).

### Krok 1 – `VR Boundary Demo › 1. Konfiguruj projekt dla Meta Quest`
Skrypt ustawia:
- Android: **IL2CPP**, **ARM64**, **min. API 32**, **Vulkan**, orientacja pozioma, identyfikator `com.vrboundarydemo.quest`,
- **IL2CPP Code Generation = Faster (smaller) builds** – szybsza kompilacja (opis w [punkcie 4](#przyspieszenie-kompilacji)),
- **Linear** color space,
- **URP**: tworzy `VRBoundaryDemo_URP.asset` (MSAA 4×, bez HDR i bez tekstur głębi/koloru – ustawienia przyjazne dla Quest) i przypina go w Graphics/Quality,
- w *Quest Features*: **Hand Tracking Support = Controllers Only** (demo działa tylko na kontrolerach) oraz **Passthrough Support = Supported** (wymagane przez Boundary API),
- **Development Build + Script Debugging**,
- na koniec proponuje **przełączenie platformy na Android** – zgódź się (trwa kilka minut).

### Krok 2 – XR Plug-in Management (ręcznie, jednorazowo)
1. **Edit › Project Settings › XR Plug-in Management**.
2. Zakładka **Android** (ikona robota): zaznacz **OpenXR**, a poniżej grupę funkcji **Meta XR**.

### Krok 3 – Project Setup Tool
**Meta › Tools › Project Setup Tool**. Na zakładce **Android** kliknij **Fix All**, potem **Apply All**. Powtarzaj, aż nie zostaną błędy wymagane („Required”).

### Krok 4 – `VR Boundary Demo › 2. Zbuduj scenę demo`
Tworzy `Assets/VRBoundaryDemo/Scenes/BoundaryDemo.unity` (podłoga z siatką co 1 m, słupki odniesienia, panele, obiekty granic i logika demo) i dodaje ją do listy scen builda. Scena nie ma jeszcze kamery VR – tę dają bloki w następnym kroku.

### Krok 5 – Building Blocks
Otwórz **Meta › Tools › Building Blocks**. Dla każdego bloku z tabeli: **kliknij w puste miejsce w Hierarchy** (nic nie może być zaznaczone), znajdź kafelek i kliknij **Add Block**. Zależności dodają się same.

| Blok | Po co | Wymagany |
|---|---|---|
| **Camera Rig** | kamera VR, `OVRManager` (granica, układ odniesienia), punkty zaczepienia kontrolerów | tak |
| **Controller Tracking** | modele kontrolerów | zalecany |
| **Passthrough** | obraz z kamer gogli – potrzebny do przycisków **X** i **Y** (ukrywanie granicy działa tylko przy passthrough) | zalecany |

Bloku **Hand Tracking** nie dodawaj – demo obsługuje wyłącznie kontrolery.

### Krok 6 – `VR Boundary Demo › 3. Sprawdź bloki w scenie`
Pokazuje raport `[OK]` / `[BRAK]` dla każdego bloku, ostrzega przed zdublowanym rigiem lub zbędną kamerą, ustawia w `OVRManager` **Tracking Origin = Floor Level** i włącza passthrough w `OVRManager` (*Enable Passthrough*), jeśli jest blok Passthrough. Gdy wszystko jest `[OK]`, zapisz scenę (**Ctrl+S**).

> Ponowne uruchomienie kroku 4 (np. po zmianach w skryptach) **odświeża** tylko obiekty demo – Building Blocks zostają w scenie.

---

## 4. Build & Run przez USB

### Przygotowanie gogli (jednorazowo)
1. Aplikacja **Meta Horizon** na telefonie › Urządzenia › Quest 3S › **Ustawienia gogli › Tryb dewelopera** › włącz.
2. W goglach ustaw granicę w trybie **Roomscale** (Ustawienia › Granica › Utwórz granicę / Obszar gry). Przy granicy **stacjonarnej** obszar gry jest bardzo mały i trudniej przetestować demo.
3. Podłącz gogle kablem USB-C do komputera. W goglach pojawi się pytanie **„Zezwolić na debugowanie USB?”** – zaznacz *Zawsze zezwalaj z tego komputera* i potwierdź.
4. Sprawdź połączenie w Unity: **File › Build Profiles › Android** › lista **Run Device** › **Refresh** – gogle powinny pojawić się na liście.

### Build & Run
**Sposób A (najprostszy):** `VR Boundary Demo › 4. Build And Run (USB)`
– sprawdza bloki w scenie, buduje `Builds/VRBoundaryDemo.apk` (Development + Script Debugging), instaluje na goglach i uruchamia. W Console pojawia się czas budowania i aktualny tryb IL2CPP.

**Sposób B (standardowy):** **File › Build Profiles**
1. Wybierz platformę **Android** (lub profil **Meta Quest**) – musi być aktywna.
2. **Run Device**: wybierz Quest 3S (przycisk *Refresh*, jeśli go nie widać).
3. Zaznacz **Development Build** i **Script Debugging**.
4. **Build And Run** › podaj nazwę pliku APK (np. w folderze `Builds/`).

Aplikacja uruchomi się w goglach automatycznie. Później znajdziesz ją w **Biblioteka › Nieznane źródła** (*Unknown Sources*).

### Przyspieszenie kompilacji
Najdłuższą częścią budowania dla Questa jest **IL2CPP**: kod C# jest tłumaczony na C++ i kompilowany kompilatorem natywnym. Krok 1 ustawia:

**Edit › Project Settings › Player › Android › Other Settings › Configuration › IL2CPP Code Generation = Faster (smaller) builds**

(w kodzie: `PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android, Il2CppCodeGeneration.OptimizeSize)`)

- IL2CPP generuje **mniej kodu C++** – typy generyczne korzystają ze wspólnej implementacji zamiast osobnej kopii dla każdego typu, więc kompilacja natywna i plik APK są mniejsze, a build wyraźnie szybszy.
- Koszt: kod generyczny działa minimalnie wolniej – dla tego demo niezauważalne.
- Do wersji końcowej możesz wrócić do **Faster runtime**: menu **VR Boundary Demo › Kompilacja IL2CPP: przełącz szybsze buildy ↔ szybsze działanie** (to samo pole w Player Settings).

Dodatkowo kolejne buildy są szybsze od pierwszego, bo Unity zapamiętuje wyniki w folderze `Library` – nie usuwaj go bez potrzeby.

---

## 5. Instrukcja obsługi w goglach

### Co widać po starcie
Stoisz na turkusowym znaczniku w wirtualnym pokoju z siatką (kratka = 1 m). Wokół Ciebie:
- **niebieska linia na podłodze** – granica systemowa (Play Area) odczytana z gogli,
- przed Tobą **panel pomocy**, z lewej **panel stanu granic**, z prawej **dziennik zdarzeń**.

Ściany granicy są prawie niewidoczne i **pojawiają się, gdy zbliżasz głowę lub kontroler** do krawędzi. Przy krawędzi kontroler **wibruje** (tym mocniej, im bliżej), w miejscu najbliższej krawędzi pojawia się czerwona kulka, a przed oczami – ostrzeżenie z odległością.

### Prawy kontroler – własna granica

| Wejście | Akcja |
|---|---|
| **Trigger** | Dodaj narożnik własnej granicy w miejscu pomarańczowego celownika na podłodze |
| **Grip** (krótko) | Cofnij ostatni punkt |
| **Grip** (przytrzymaj 1 s) | Wyczyść własną granicę |
| **A** | Testy bliskości: **systemowy Play Area ↔ własna granica** |
| **B** | Pokaż/ukryj wizualizację Play Area |
| **Gałka ←/→** | Obrót o 45° |
| **Klik gałki** | Powrót na start |

Własna granica jest zamknięta od 3 punktów (ostatni łączy się z pierwszym). Po naciśnięciu **A** wszystkie ostrzeżenia, wibracje i wpisy w dzienniku dotyczą narysowanej granicy – możesz np. wyznaczyć mniejszy obszar wewnątrz Play Area i sprawdzić, jak aplikacja pilnuje własnych stref.

### Lewy kontroler – system

| Wejście | Akcja |
|---|---|
| **X** | Passthrough wł./wył. (wirtualna podłoga znika – granice rysowane są na prawdziwej podłodze) |
| **Y** | Poproś system o **ukrycie granicy systemowej** (działa tylko przy włączonym passthrough) |
| **Trigger** | Odczytaj granicę systemową ponownie |
| **Grip** (trzymaj) | Pokaż wszystkie ściany granic w pełni |
| **Menu ☰** | Układ odniesienia **Floor Level ↔ Stage** |
| **Gałka** | Ruch w scenie |
| **Klik gałki** | Pokaż/ukryj panele |

### Scenariusze do sprawdzenia
1. **Podejdź do krawędzi** obszaru gry – ściana się pojawia, kontroler wibruje, w dzienniku: *blisko krawędzi* → *POZA granicą*. Porównaj z tym, kiedy pojawia się granica systemowa.
2. **Wyjedź gałką** w drugi koniec sceny – niebieska linia jedzie razem z Tobą. Granica należy do **fizycznego pokoju**, nie do wirtualnej sceny.
3. **Narysuj własną granicę** (trigger), przełącz testy na nią (**A**) i przejdź przez jej krawędź.
4. **X**, potem **Y** – passthrough i prośba o ukrycie granicy. Panel pokazuje *prośbę* i *faktyczny stan* (decyduje system). Wyłączenie passthrough (**X**) automatycznie cofa prośbę – bez passthrough system i tak nie ukryje granicy.
5. **Menu ☰** – przełącz na **Stage**: początek układu przenosi się na środek obszaru gry (scena „przeskakuje”), granica zostaje na miejscu, a własna granica jest przeliczana.
6. **Przytrzymaj przycisk Meta** (wyśrodkowanie widoku) – w dzienniku pojawi się wpis o recenter i ponownym odczycie granicy.

Na panelu **GRANICE** widać na żywo: czy granica jest skonfigurowana, rozmiar i pole Play Area, układ odniesienia, stan passthrough i ukrywania granicy, liczbę punktów własnej granicy oraz strefę i odległość od krawędzi dla głowy i obu kontrolerów.

---

## 6. Jak to działa

### 6.1 Architektura

```
OVRManager ──► OVRManager.boundary (OVRBoundary)          OVRInput (przyciski, gałki, wibracje)
     │                 │                                          │
     │          SystemBoundary ──► BoundaryShape ◄── CustomBoundary (promień + trigger)
     │          (odczyt, odświeżanie)    │                         │
     │                                   ▼                         │
     │                           BoundaryMonitor ───────► Haptics (wibracje przy krawędzi)
     │                (głowa + kontrolery: strefa, odległość, HUD, dziennik)
     │                                   │
     ├─ shouldBoundaryVisibilityBeSuppressed / isBoundaryVisibilitySuppressed
     │          PassthroughBoundaryControl (X, Y)          BoundaryVisual (linie i ściany)
     └─ trackingOriginType  ◄── DemoActions (mapowanie przycisków)
```

### 6.2 Przestrzeń śledzenia – najważniejsza idea
`OVRCameraRig` ma obiekt **TrackingSpace**, w którym system ustawia głowę (`CenterEyeAnchor`) i kontrolery. **W tym samym układzie** `OVRBoundary` podaje punkty granicy. Dlatego:
- wizualizacja granicy jest **dzieckiem `trackingSpace`** – wystarczy wstawić punkty jako współrzędne lokalne,
- testy bliskości przeliczają pozycję głowy i kontrolerów do układu `trackingSpace` (`InverseTransformPoint`) i porównują z wielokątem w płaszczyźnie XZ,
- przesuwanie gracza gałką przesuwa cały rig, a z nim granicę – ona opisuje pokój, nie scenę.

### 6.3 Odczyt granicy systemowej – `OVRBoundary`
```csharp
OVRBoundary boundary = OVRManager.boundary;
bool configured = boundary.GetConfigured();                               // czy są dane granicy
Vector3[] pts   = boundary.GetGeometry(OVRBoundary.BoundaryType.PlayArea); // narożniki, przestrzeń śledzenia, y = 0
Vector3 size    = boundary.GetDimensions(OVRBoundary.BoundaryType.PlayArea); // szerokość, 0, głębokość
```
**Play Area** to prostokąt wpisany w granicę narysowaną przez użytkownika. W backendzie OpenXR przestarzałe są: `BoundaryType.OuterBoundary`, `TestNode()`, `TestPoint()`, `GetVisible()`, `SetVisible()` – demo ich nie używa. Testy „czy jestem w środku i jak daleko do krawędzi” liczy samo (`BoundaryShape`).

Meta zaleca ponowne sprawdzanie granicy po wznowieniu aplikacji. `SystemBoundary` odczytuje ją: przy starcie, co 2 s, po założeniu gogli (`OVRManager.HMDMounted`), po odzyskaniu śledzenia (`OVRManager.TrackingAcquired`), po wznowieniu (`OnApplicationPause(false)`), po wyśrodkowaniu widoku (`OVRManager.display.RecenteredPose`) i po zmianie układu odniesienia.

### 6.4 Testy bliskości – `BoundaryShape` i `BoundaryMonitor`
- **W środku czy poza:** test parzystości przecięć (promień z punktu w kierunku +X przecina krawędzie wielokąta nieparzystą liczbę razy → punkt jest w środku).
- **Odległość od krawędzi:** minimum odległości punktu od każdego odcinka (rzut punktu na odcinek).
- **Strefy:** *poza* (poza wielokątem), *blisko krawędzi* (głowa < 0,4 m, kontroler < 0,3 m), *bezpiecznie*.
- **Reakcje:** wibracja `OVRInput.SetControllerVibration` – od 0,08 do 0,6 w strefie ostrzeżenia, 0,8 poza granicą, mocny impuls przy przekroczeniu; czerwony znacznik w najbliższym punkcie krawędzi; panel ostrzegawczy przed oczami; wpis w dzienniku przy każdej zmianie strefy.
- **Ściany:** przezroczystość każdej ściany zależy od najbliższego śledzonego punktu (pełna widoczność przy odległości 0, zanik przy 0,9 m).

### 6.5 Własna granica – `CustomBoundary`
Promień z prawego kontrolera jest przecinany z **płaszczyzną podłogi przestrzeni śledzenia** (`Plane(trackingSpace.up, trackingSpace.position)`) – bez fizyki i koliderów. Trigger zapisuje punkt w układzie `trackingSpace`, więc własna granica, tak jak systemowa, „przykleja się” do pokoju.

Po wyśrodkowaniu widoku albo zmianie Floor Level ↔ Stage układ śledzenia przeskakuje względem pokoju. Skrypt wykrywa skok pozy głowy (głowa fizycznie nie przesunie się o 25 cm lub 20° w jednej klatce) i przelicza punkty tym samym obrotem i przesunięciem.

### 6.6 Passthrough i ukrywanie granicy – `PassthroughBoundaryControl`
```csharp
passthroughLayer.enabled = true;                                  // blok Passthrough (OVRPassthroughLayer)
OVRManager.instance.shouldBoundaryVisibilityBeSuppressed = true;  // prośba o ukrycie granicy
bool hidden = OVRManager.instance.isBoundaryVisibilitySuppressed; // faktyczny stan (decyduje system)
OVRManager.BoundaryVisibilityChanged += v => Debug.Log(v);        // zdarzenie zmiany widoczności
```
- System ukrywa granicę **tylko przy aktywnym passthrough** – inaczej prośba jest ignorowana, a `OVRManager` ponawia ją co klatkę. Dlatego demo wyłącza prośbę razem z passthrough.
- Wymaga **Passthrough Support ≠ None** w *OVRManager › Quest Features* (ustawia krok 1).
- Passthrough to warstwa **pod** obrazem aplikacji: tło kamery musi być przezroczyste (`Color(0,0,0,0)`), a nieprzezroczysta podłoga ukryta – robi to skrypt.
- Do aplikacji w całości bez granicy (tylko MR, bez treści immersyjnej) służy wpis `com.oculus.feature.BOUNDARYLESS_APP` w `AndroidManifest.xml` – demo go **nie** używa, bo jest aplikacją VR.

### 6.7 Układ odniesienia – Floor Level i Stage
| | Floor Level | Stage |
|---|---|---|
| Początek układu | miejsce wyśrodkowania widoku, na podłodze | środek obszaru gry, na podłodze |
| Osie | kierunek patrzenia przy wyśrodkowaniu | równoległe do krawędzi obszaru gry |
| Wyśrodkowanie widoku | przesuwa układ | nie zmienia układu |

Zmiana w kodzie: `OVRManager.instance.trackingOriginType = OVRManager.TrackingOrigin.Stage;`

### 6.8 Mapowanie przycisków – `DemoActions`
```csharp
OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch); // prawy trigger
OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger,  OVRInput.Controller.RTouch); // prawy grip
OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch);  // A   (z LTouch: X)
OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch);  // B   (z LTouch: Y)
OVRInput.GetDown(OVRInput.Button.Start, OVRInput.Controller.LTouch); // Menu ☰
OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch); // lewa gałka
OVRInput.SetControllerVibration(0.25f, 0.5f, OVRInput.Controller.RTouch);    // częstotliwość, amplituda
```
Wibracja w `OVRInput` wygasa sama po ok. 2 s, dlatego `Haptics` odświeża ciągły poziom co 0,5 s, a wyłącza go wartościami `(0, 0)`.

---

## 7. Dostrajanie i rozbudowa

**Progi ostrzeżeń** – obiekt `Demo` › komponent **Boundary Monitor**: *Head Warn Distance*, *Controller Warn Distance*; tam też wyłączysz wibracje, znaczniki i ostrzeżenie przed oczami.

**Wygląd granic** – obiekty `Boundaries/SystemBoundary` i `Boundaries/CustomBoundary` › komponent **Boundary Visual**: kolor, wysokość ścian, *Fade Distance*, przezroczystość.

**Siła wibracji** – `Demo` › **Haptics**: *Master Amplitude*, częstotliwości.

**Rysowanie** – `Boundaries/CustomBoundary` › **Custom Boundary**: ręka rysująca, minimalny odstęp i limit punktów.

**Nowa akcja na przycisk** – w `DemoActions.Update()`:
```csharp
if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch))
    DemoLog.Add("Moja akcja!");
```

**Reakcja na wyjście poza granicę** – w `BoundaryMonitor.LogZoneChange()` (przypadek `BoundaryZone.Outside`) możesz np. zatrzymać grę albo przyciemnić scenę.

**Odświeżenie sceny** – po zmianach w `VRBoundaryDemoSetup.cs` uruchom ponownie *VR Boundary Demo › 2. Zbuduj scenę demo*. Obiekty demo powstaną od nowa, a Building Blocks zostaną.

---

## 8. Rozwiązywanie problemów

Zacznij od **VR Boundary Demo › 3. Sprawdź bloki w scenie** – wskazuje większość problemów ze sceną.

| Problem | Rozwiązanie |
|---|---|
| Gogle nie są widoczne w *Run Device* | Kabel z transmisją danych, potwierdź „Zezwól na debugowanie USB” w goglach, sprawdź tryb dewelopera, kliknij *Refresh*. |
| Aplikacja startuje jako płaskie okno 2D | Nie zaznaczono **OpenXR + Meta XR** w *XR Plug-in Management › Android* (krok 2) albo brak bloku **Camera Rig**. |
| Panel: „granica nieskonfigurowana” / brak niebieskiej linii | Ustaw granicę w goglach (Ustawienia › Granica). W edytorze bez gogli granicy nie ma – to normalne. |
| Play Area jest bardzo mały | Granica stacjonarna – przełącz na **Roomscale** i narysuj większy obszar. |
| Linia granicy nie pokrywa się z podłogą / jest przesunięta | Uruchom *3. Sprawdź bloki* (Floor Level), wyśrodkuj widok (przytrzymaj przycisk Meta) albo naciśnij lewy trigger. |
| **Y** nie ukrywa granicy | Najpierw **X** (passthrough). Sprawdź *OVRManager › Quest Features › Passthrough Support* (≠ None), *Enable Passthrough* w OVRManager i aktualny Horizon OS. |
| **X** nic nie robi | Brak bloku **Passthrough** – dodaj go i uruchom *3. Sprawdź bloki*. |
| Czarne tło przy passthrough | W *OVRManager* zaznacz *Enable Passthrough* (robi to krok *3. Sprawdź bloki*). |
| Brak wibracji | Kontroler musi być aktywny (nie w trybie dłoni); sprawdź *Haptics › Master Amplitude*. |
| Obraz zdublowany / dwie kamery | W scenie są dwa rigi lub zostawiona *Main Camera* – zostaw jeden blok Camera Rig. |
| Czarny ekran / błędy URP | Uruchom ponownie krok 1 (przypina URP) i *Project Setup Tool › Fix All*. |
| Build trwa bardzo długo | Sprawdź *IL2CPP Code Generation = Faster (smaller) builds* ([punkt 4](#przyspieszenie-kompilacji)); nie usuwaj folderu `Library`. |
| Brak logów w Console | Build musi być *Development*; w Console wybierz gogle z listy połączeń albo użyj okna *Android Logcat* (filtr `VRBoundaryDemo`). |

---

### Przydatne linki
- [Meta – Boundary (OVRBoundary)](https://developers.meta.com/horizon/documentation/unity/unity-ovrboundary/)
- [Meta – Boundaryless i Boundary API](https://developers.meta.com/horizon/documentation/unity/unity-boundaryless/)
- [Meta – API OVRBoundary](https://developers.meta.com/horizon/reference/unity/v207/class_o_v_r_boundary) · [OVRManager](https://developers.meta.com/horizon/reference/unity/v207/class_o_v_r_manager)
- [Meta – Building Blocks](https://developers.meta.com/horizon/documentation/unity/bb-overview/)
- [Meta – ręczna konfiguracja (OpenXR, Project Setup Tool)](https://developers.meta.com/horizon/documentation/unity/unity-tutorial-hello-world-manual-steps/)
