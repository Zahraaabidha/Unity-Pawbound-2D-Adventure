# 🐾 Pawbound — 2D Endless Runner

A 2D endless-runner game built with **Unity and C#**, inspired by classic browser-based runner games.

The player guides a character through a continuously scrolling environment, jumping over obstacles and surviving for as long as possible while the game progressively increases in difficulty.

![Pawbound Gameplay](public/picture.png)

---

## 🎮 Gameplay

Pawbound is a fast-paced 2D endless runner focused on simple, responsive gameplay.

The player automatically moves through a side-scrolling environment and must jump over incoming obstacles. The longer the player survives, the higher the score and the more challenging the game becomes.

### Core Gameplay Loop

```text
        Start Game
            ↓
      Character Runs
            ↓
      Obstacles Appear
            ↓
       Jump / Avoid
            ↓
        Earn Score
            ↓
    Difficulty Increases
            ↓
        Keep Running
            ↓
         Collision
            ↓
        Game Over
            ↓
          Restart
