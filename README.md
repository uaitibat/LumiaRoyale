# LumiaRoyale

Esqueleto inicial de um jogo original de arena com cartas para **Windows 10 Mobile (Lumia 640 / ARM32)**, feito com **C# + UWP + MonoGame**.

> Projeto original, inspirado apenas na lógica de gênero (cartas, mana, torres, arena). Não usa nomes, personagens, arte ou assets de Clash Royale/Supercell.

## Estado atual

- UWP ARM32: `TargetPlatformVersion 10.0.19041.0`, `TargetPlatformMinVersion 10.0.15063.0`
- Orientação retrato
- Resolução real da tela via `DisplayInformation.RawPixelsPerViewPixel` (sem hardcode 480x800)
- Loop fixo ~30 FPS
- Arena placeholder com 3 torres por lado
- Mana, 4 cartas originais, unidades, combate básico e IA simples
- CI GitHub Actions: compila ARM, assina com `certs/ci-legacy.b64` e publica `.appx` em `latest`

## Build CI

Workflow em `.github/workflows/build-appx.yml`, no padrão dos outros projetos Lumia.
