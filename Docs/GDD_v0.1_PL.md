# MOURNEVEIL: THE ETERNAL REIGN — Mini GDD v0.1

**Status:** Preprodukcja / pierwszy prototyp  
**Zespół:** Mariia Dorokhova i Szymon Drzazga
**Silnik:** Unity, C#; 2D UI z ilustracyjnym tłem  
**Platforma docelowa:** PC  
**Wersja dokumentu:** 0.1, 17 listopada 2026

## 1. High concept

Ostatni dziedzic starożytnego rodu Mourneveil budzi się po kilkuset latach w zrujnowanej krypcie. Odbudowując gospodarkę krwi, odzyskuje władzę nad zamkiem. Wraz z jego potęgą budzi się też coś, co od dawna mieszka w murach.

**Unikalny wyróżnik:** postęp ekonomiczny zwiększa aktywność horroru. Gracz świadomie wymienia bezpieczeństwo na szybszą produkcję.

**Robocze motto:** *Blood remembers what time forgets.*

## 2. Filary gry

1. **Satysfakcjonująca progresja** — każde ulepszenie ma odczuwalny efekt.
2. **Gotycki świat** — stary zamek, arystokracja, tajemnice rodu.
3. **Horror systemowy** — zdarzenia zależą od progresji, a nie wyłącznie od zegara.
4. **Odkrywanie historii** — kolejne cykle odsłaniają przeszłość bohatera.

## 3. Core loop

Zbieranie krwi → zakup ulepszenia → produkcja automatyczna → wzrost Awakening → wydarzenie horrorowe → dalszy rozwój. W późniejszej wersji: wielowiekowy sen / prestiż.

## 4. Zakres pierwszego prototypu (Sprint 0)

### Pierwsza lokacja: Krypta

- **Blood:** główna waluta.
- **Awakening:** wskaźnik 0–100, nie waluta.
- **Gather Blood:** +1 krwi na kliknięcie na starcie.
- **Stronger Bite:** podnosi krew/kliknięcie o +1 za poziom. Cena: `ceil(10 × 1.6^level)`.
- **Restless Servant:** +0.5 krwi/sekundę na poziom. Cena: `ceil(15 × 1.7^level)`.
- **Awakening:** +0.3 za kliknięcie, +3 za Stronger Bite, +6 za Restless Servant, dodatkowo powoli rośnie wraz z produkcją pasywną.
- **Pierwsze wydarzenie:** po 35 Awakening pojawia się komunikat „The walls whisper your name...”. Odtwarza się jednokrotnie i zapisuje stan.
- **Save:** lokalny JSON, zapis ręczny, automatyczny co 30 s i przy wyjściu.

### Definicja ukończenia

Gracz potrafi zebrać krew, kupić obydwa ulepszenia, obserwować pasywną produkcję, uruchomić event, zapisać i odzyskać stan gry po restarcie.

### Czego jeszcze NIE ma

Prestige, pokoje poza kryptą, pełny horror director, animacje, fabuła, dźwięk, offline progress, duże liczby, finalny balans, ekran tytułowy.

## 5. Planowany rozwój po prototypie

- **Krypta:** pierwsze źródła krwi i słudzy.
- **Biblioteka:** wiedza i fragmenty wspomnień.
- **Sala Tronowa:** wpływy dynastii i zarządzanie rodem.
- **Zakazana Komnata:** ryzykowne rytuały i silniejsze anomalie.
- **Wielowiekowy Sen:** system prestiżu, trwałe ulepszenia i rozwój narracji.

To szkic, nie zatwierdzone wszystkie mechaniki.

## 6. Styl wizualny i audio

2D gothic dark UI, czerń / granat / burgund / przygaszone złoto. Uwaga na czytelność kontrastu, skalowanie UI i opcję ograniczania nagłych efektów horroru. Jumpscare'y oszczędnie, po zbudowaniu napięcia.

## 7. Nierozstrzygnięte decyzje

- Imię, płeć, wygląd oraz charakter konkretnie zdefiniowanego protagonisty.
- Czy historia jest liniowa, czy ma wybory i zakończenia.
- Czy ostateczny tytuł będzie „Mourneveil: The Eternal Reign”.
- Jak działa głęboki system prestiżu, balans mid/late-game oraz offline progress.
- Kto odpowiada za poszczególne zadania w dwójce — do ustalenia wspólnie.

## 8. Pierwsze kryteria jakości

- Po 60 sekundach widać sens kolejnego ulepszenia.
- Bez błędów po zapisie / ponownym uruchomieniu.
- Rozgrywka nie wymaga ciągłego klikania po zakupie sługi.
- Horror jest zapowiadany subtelną zmianą wskaźnika Awakening.
- Kod jest podzielony na stan, logikę, zapis i UI.
