echo off
cls
C:\Programs\Texconv\texconv.exe *_cm.png -nologo -y -f BC7_UNORM_SRGB -if TRIANGLE_DITHER_DIFFUSION -sepalpha
C:\Programs\Texconv\texconv.exe *_ng.png -nologo -y -f BC7_UNORM_SRGB
C:\Programs\Texconv\texconv.exe *_add.png -nologo -y -f BC7_UNORM_SRGB -if TRIANGLE_DITHER_DIFFUSION -sepalpha
pause