# LumiaRoyale

Esqueleto inicial de um jogo original de arena com cartas para **Windows 10 Mobile (Lumia 640 / ARM32)**, feito com **C# + UWP + MonoGame**.

> Projeto original, inspirado apenas na lógica de gênero (cartas, mana, torres, arena). Não usa nomes, personagens, arte ou assets de Clash Royale/Supercell.

## Estado atual

- UWP ARM32: `TargetPlatformVersion 10.0.19041.0`, `TargetPlatformMinVersion 10.0.15063.0`
- Orientação retrato
- Resolução real da tela via `DisplayInformation.RawPixelsPerViewPixel` (sem hardcode 480x800)
- Loop fixo ~30 FPS
- Arena com cenário florestal próprio (`Assets/ArenaForest.jpg`), rio e duas pontes; 3 torres por lado
- Mana, coleção de 30 unidades originais (6 corpo a corpo, 5 atiradores, 4 tanques, 4 rápidas, 4 mágicas, 3 aéreas, 4 especiais) com deck de 8 escolhido na home
- Combate básico, torres com nível, baús e IA (também na demonstração da home)
- Sprites CC0: Puny Characters + extensão Orcs (18 personagens com idle, andar, ataque, dano e morte em 8 direções; licença em `Assets/Sprites/Puny/LICENSE.txt`)
- CI GitHub Actions: compila ARM, assina com `certs/ci-legacy.b64` e publica `.appx` em `latest`

## Build CI

Workflow em `.github/workflows/build-appx.yml`, no padrão dos outros projetos Lumia.
