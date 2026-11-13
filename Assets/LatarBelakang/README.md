# Latar Belakang Sawah — Import dari Blender ke Unity

Asset berasal dari:
`C:\TUGAS-TUGAS\TUGAS SEMESTER 5\tugas\Asset\Latar Belakang\Latar Background fiks - backup 2026-11-03 sebelum populasi.blend` (Blender 5.2).

File `.blend` **tidak diubah** sama sekali. Konversi dilakukan lewat export FBX
(Blender headless) lalu diimpor ke Unity 2021.3 (Built-in Render Pipeline).

> Versi ini menggantikan import sebelumnya (`Latar Background fiks.blend`).
> Isinya: environment sawah + **20 rumah kampung** (Grup 1–4) + langit/awan.
> **Tidak ada hewan, orangan, atau pekerja** — memang belum ada di file backup ini.

## Struktur folder

```
Assets/LatarBelakang/
├─ Models/                 ENV_ModulSawah.fbx, POP_RumahKampung.fbx, SKY_Awan.fbx
│  └─ Materials/           material hasil import (di-remap ke shader Standard)
├─ Textures/               25 tekstur PNG (hasil ekstrak dari packed .blend)
├─ Materials/              Sky_LatarBelakang.mat (skybox gradient)
├─ Prefabs/                ENV_ModulSawah, POP_RumahKampung, SKY_Awan
├─ Scripts/                CloudDrift.cs, WaterUVScroll.cs
├─ Shaders/                SkyGradient.shader
├─ Data/sceneinfo.json     dump material + transform kamera/matahari dari Blender
└─ Editor/LatarBelakangSetup.cs   menu pipeline import
```

## Menu Editor

`Tools > Latar Belakang`

1. **Import Model & Material** — set import setting, patch material dari data Blender, buat skybox.
2. **Pasang Di Scene** — buat prefab, pasang ke scene aktif, static batching, atur kamera/cahaya/langit.
3. **Screenshot Preview** — render 4 sudut ke `<project>/../opencode-lb-shots/`.
4. **Pasang Collision & Play Area** — collision mesh tanah, safety net, pagar batas, trigger air, spawn point.
5. **Verifikasi Collision** — raycast grid 2.288 titik, log cakupan tanah.
6. **Buat Player VR** — prefab + rig pemain VR/desktop.
7. **Pasang Aset Tambahan** — petani, kerbau, ayam, orangan.
9. **Snap Aset Tambahan ke Tanah** — menempelkan ulang aktor ke permukaan tanah.
10. **Pasang Elang di Langit** — import `BUR_Elang`, buat prefab, pasang 12 elang yang berputar di langit.
11. **Pasang Hiasan Perumahan** — pagar, jemuran, warung, jerami, bangku, meja, pot, lampu, bendera, gerobak, plus ayam & pekerja.

Tambahan: **Test Lighting** (uji variasi pencahayaan) dan **Diagnostik Object** (log object paling besar).

Aman dijalankan ulang: root `LatarBelakang` lama akan diganti.

## Isi scene

| Prefab | Isi | Object |
|---|---|---|
| `ENV_ModulSawah` | lahan, padi, air, 7 jalur, air terjun, aliran sungai | 322 |
| `POP_RumahKampung` | 20 rumah (Grup 1–4) + jalan tiap grup | 20 |
| `SKY_Awan` | 8 awan (CloudDrift) | 8 |

Total ±320k triangle, 54 material, skala **1 unit = 1 meter** (sama dengan Blender).

## Elang di Langit

Aset `BUR_Elang` diimpor dari `...\tugas\Asset\BUR_Elang` (FBX + atlas 512).
FBX tidak berisi animation clip, jadi gerak dibuat prosedural oleh `ElangFlight.cs`:

- Orbit melingkar dengan radius, ketinggian, kecepatan, arah putar, dan fase acak.
- Sayap mengepak lewat rotasi bone `wing_L/R_humerus`, `forearm`, dan `hand`.
- Badan miring (bank) saat berputar dan naik-turun halus.
- 12 elang diskalakan **2×** (wingspan ±2,5 m) di root `Elang_Langit`.
- Bone yang dipakai: `wing_L_humerus`, `wing_R_humerus`, `wing_L_forearm`,
  `wing_R_forearm`, `wing_L_hand`, `wing_R_hand`.

Menu **10. Pasang Elang di Langit** mengimpor FBX, membuat material
`M_Elang_Atlas_Unity`, prefab `BUR_Elang.prefab`, lalu memasang 12 elang.
`AutoSetupElangHiasan` menjalankan pemasangan ini **sekali otomatis** setelah
compile; menu 10 tetap bisa dipakai untuk memasang ulang.

Kalau arah kepakan terlihat terbalik (satu sayap naik, satu turun), centang
`Mirror Flap` pada komponen `ElangFlight`.

## Hiasan Perumahan

Menu **11. Pasang Hiasan Perumahan** menaruh properti di halaman 20 rumah
`Rumah_01..20` di bawah `POP_RumahKampung`, dalam root scene `Hiasan_Perumahan`:

- pagar kayu, bangku, meja, pot tanaman,
- jemuran dengan kain merah/biru/kuning,
- tumpukan jerami, karung, tumpukan kayu bakar,
- lampu hias, bendera, gerobak,
- 3 warung kecil di area kampung,
- 8 ayam dan 5 pekerja tambahan.

Semua properti dibuat dari primitive low-poly, berbagi material `M_Hiasan_*`,
tanpa collider, static batching, dan shadow dimatikan untuk objek kecil.
Menu bisa dijalankan ulang; root `Hiasan_Perumahan` lama akan diganti.
Pemasangan pertama juga dijalankan otomatis oleh `AutoSetupElangHiasan`
setelah compile.

## Guncangan Kamera

`DesktopPlayerFallback` dan `VRPlayerLocomotion` menggeser `CameraOffset`
memakai Perlin noise saat **sprint + bergerak + menyentuh tanah**:

- desktop: amplitudo ±0,035 m,
- VR: ±0,014 m (sangat halus),
- frekuensi ±10–11 Hz, blend 6–8×,
- bisa dimatikan lewat `enableRunShake` di Inspector.

## Catatan konversi

- **Kubah langit 6000 m tidak diimpor** (sesuai kesepakatan). Diganti skybox
  `LatarBelakang/SkyGradient` memakai tekstur `T_Langit_Gradient_512`.
- **Animasi**: awan dianimasikan dengan `CloudDrift` (lebih hemat daripada Animator
  dan tidak butuh animation clip). Material air/foam memakai `WaterUVScroll`
  karena animasi node shader Blender tidak ikut ke Unity.
- **Kerbau tidak ada**: satu-satunya instance di `06_Hewan` memakai linked library
  `D:\KULIAH\...\KRB_Kerbau.blend` yang sudah tidak ada, jadi dilewati.
- Mesh ber-armature (kalau ada) di-bake ke **rest pose**, karena deformasi armature
  di luar file aslinya rusak. Vertex liar (spike) juga dibuang saat export.
- Material bertekstur diberi nama sesuai file tekstur oleh Unity
  (`Atlas_Rumah_01`, `T_Foam_BaseColor`, dst.) — script import memetakannya balik
  ke data material Blender.

## Collision & Play Area (siap untuk karakter)

Dibuat lewat menu **`Tools > Latar Belakang > 4. Pasang Collision & Play Area`**.

| Objek | Isi | Layer |
|---|---|---|
| `LatarBelakang/Collision/COL_Terrain` | MeshCollider dari `Models/COL_Terrain.fbx` — 11.120 vert / **14.941 tri**, di-bake dari geometri tanah saja (tanpa tanaman, atap, pohon) | Ground |
| `SafetyNet` | BoxCollider besar di `y = -60` | Barrier |
| `Boundary_N/S/E/W` | 4 dinding tak terlihat di tepi area (x ±512, z ±436) | Barrier |
| `Water_AS_Water`, `Water_AT_Water_Tier_01..03`, `Water_AT_Water_Upper` | BoxCollider **isTrigger** + `WaterVolume` | Water |

Layer baru: **8 = Ground, 9 = Water, 10 = Barrier**.
`Boundary_W`/`Boundary_E` berada di x = −512 / +520, `Boundary_S`/`Boundary_N` di z = −436 / +440.

**Hasil verifikasi (menu 5):** raycast grid 2.288 titik → **2.288/2.288 (100%) ada tanah**, tinggi permukaan y −3,2 s/d 72,7.
Uji fisik: capsule Rigidbody dijatuhkan dari y = 60 → berhenti di y = 15,1 (tanah 14,1 + radius); sphere di luar area berhenti di `SafetyNet` y = −57,5.

**Script bantuan** (`Assets/LatarBelakang/Scripts`):
- `GroundCheck.cs` — `IsGrounded`, `GroundNormal`, `SnapToGround()` untuk spawn.
- `WaterVolume.cs` — event `WaterStateChanged(collider, masukAir)`.
- `FallRespawn.cs` — kalau karakter < `killY` (−40), kembalikan ke `SpawnPoint`.

`SpawnPoint` dibuat otomatis di (0, 14.12, −140) — di jalan depan kampung.

> Catatan: permukaan air `Sawah_Air_Petak` (saluran tipis antar petak) tidak dijadikan trigger karena mesh-nya menutupi seluruh petak sehingga bounds-nya terlalu besar; hanya badan air sungai & air terjun yang punya volume air.

## Optimasi VR yang sudah dipasang

- Static batching (`BatchingStatic`) untuk environment & rumah.
- Shadow casting dimatikan untuk objek kecil (<4 m environment) — 262 renderer.
- `Read/Write` mesh off, mesh compression off (kualitas), atlas rumah diturunkan 2048 → 1024.
- GPU instancing aktif pada material.
- Kamera: near 0.05, far 4000, FOV 28.3° (setara lensa 50 mm sesuai `Kamera_Utama`).

Paket **XR Plugin / OpenXR belum dipasang** — kalau build VR sungguhan diperlukan,
langkah berikutnya: install `com.unity.xr.openxr` + `XR Plug-in Management`,
lalu ganti Main Camera dengan XR Origin.
