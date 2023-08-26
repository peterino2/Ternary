
# Architecture

*ownership graph can be rendered at play.d2lang.com*
```
GameSession -> GameState
GameState -> LocalPlayerSubsystem
GameState -> GameplayNetEngine
LocalPlayerSubsystem -> SC_Camera
LocalPlayerSubsystem -> UI_LocalPlayer
```

- GameSession.cs
    - Handles starting the server, and connecting as a client
    - Dispatches commands from server with regards to loading the level, 
    - preparing the game state etc..
    - Will eventually handle HTTP requests with Live server

- GameState.cs
    - Persistent, equivalent to the GameInstance class from unreal engine
    - Handles spawning the player(s), and general game mode information from the server
    - Primary point recieving point of RPCs from the server.
    - Contains several subsystems
        - LocalPlayerSubsystem
        - GameplayNetEngine

- LocalPlayerSubsystem.cs
    - Spawned by GameState's OnReady, owned by GameState
    - Responsible for loading the user's input configurations and any other config files
    from the user.

- GameplayNetEngine.cs
    - Custom networking and reconciliation engine,
    - Rollback N-2 input and movement system.

*ownership graph can be rendered at play.d2lang.com*
```
GameSession -> GameState
GameState -> LocalPlayerSubsystem
GameState -> GameplayNetEngine
LocalPlayerSubsystem -> SC_Camera
LocalPlayerSubsystem -> UI_LocalPlayer
```

*Messaging Graph*
