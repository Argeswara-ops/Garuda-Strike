# Garuda Strike

Garuda Strike adalah game FPS single-player yang dibuat dengan Unity. Hadapi musuh AI, pilih senjata, dan bertahan selama mungkin.

## Fitur

- Dua mode permainan: Survival (5 gelombang, termasuk bos di gelombang terakhir) dan Time Attack (5 menit).
- Tiga tingkat kesulitan.
- Dua arena: Gudang Logistik dan Kota Gurun.
- Empat senjata: pistol, senapan serbu, shotgun, dan sniper.
- Granat, pisau, stamina, serta pengaturan sensitivitas mouse.

## Persyaratan

- Unity **6000.6.8f1**
- Windows dengan keyboard dan mouse

## Menjalankan project

1. Clone repository ini atau unduh sebagai ZIP.
2. Buka folder project melalui Unity Hub menggunakan Unity 6000.6.8f1.
3. Buka scene `Assets/Scenes/SampleScene.unity`.
4. Tekan **Play** di Unity Editor.

Unity Package Manager akan mengunduh dependency yang didefinisikan di `Packages/manifest.json`.

## Kontrol

| Tombol | Aksi |
| --- | --- |
| `W` `A` `S` `D` | Bergerak |
| Mouse | Melihat / mengarahkan bidikan |
| Klik kiri | Menembak |
| Klik kanan | Membidik |
| `Space` | Melompat |
| `Shift` | Berlari |
| `Ctrl` atau `C` | Jongkok |
| `R` | Mengisi ulang amunisi |
| `1`–`4` atau scroll mouse | Mengganti senjata |
| `G` | Melempar granat |
| `V` | Menggunakan pisau |
| `Esc` | Jeda |

## Struktur project

- `Assets/GarudaStrike/Scripts/` — kode gameplay, pemain, musuh, senjata, dan pembuatan arena.
- `Assets/Scenes/SampleScene.unity` — scene utama.
- `Packages/manifest.json` — daftar package Unity.
