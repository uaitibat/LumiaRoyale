# LumiaRoyale

Esqueleto inicial de um jogo original de arena com cartas para **Windows 10 Mobile (Lumia 640 / ARM32)**, feito com **C# + UWP + MonoGame**.

> Projeto original, inspirado apenas na lógica de gênero (cartas, mana, torres, arena). Não usa nomes, personagens, arte ou assets de Clash Royale/Supercell.

## Estado atual

- UWP ARM32: `TargetPlatformVersion 10.0.19041.0`, `TargetPlatformMinVersion 10.0.15063.0`
- Orientação retrato
- Resolução real da tela via `DisplayInformation.RawPixelsPerViewPixel` (sem hardcode 480x800)
- Loop fixo ~30 FPS
- 2 arenas próprias em tela cheia (`Assets/ArenaForest.png` e `Assets/ArenaDesert.png`), cada uma com rio e duas pontes alinhados à lógica; escolha na home tocando na mini arena (opção salva no dispositivo); 3 torres por lado
- Mana, coleção de 30 unidades originais (6 corpo a corpo, 5 atiradores, 4 tanques, 4 rápidas, 4 mágicas, 3 aéreas, 4 especiais) com deck de 8 escolhido na home
- Combate básico, torres com nível, baús com reset a cada 6h e IA com escolha de lane independente (também na demonstração da home)
- Sprites CC0: Puny Characters + extensão Orcs (18 personagens, 8 direções) e PunyMonsters (9 criaturas distintas: pantera, cachorro, javalis, aranhas, lobo, rato e gato). Todos com idle, andar, ataque e dano; licenças em `Assets/Sprites/Puny/LICENSE.txt` e `Assets/Sprites/Puny/Monsters/LICENSE.txt`
- CI GitHub Actions: compila ARM, assina com `certs/ci-legacy.b64` e publica `.appx` em `latest`

## Build CI

Workflow em `.github/workflows/build-appx.yml`, no padrão dos outros projetos Lumia.
