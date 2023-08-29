
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
        - GameNetEngine

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
GameState -> GameNetEngine
LocalPlayerSubsystem -> SC_Camera
LocalPlayerSubsystem -> UI_LocalPlayer
```

*Messaging Graph*



# Connection Flow

- Server starts up
- Server goes into lobby mode and waits until admin initiates game start or if any other settings are met.

- Client connects, sends client information
- [Live Service] Server pulls client information from Live Service, validates client
- Client GameState goes into Lobby mode.
- Server sends GameState info over to client

- Server Sends prepare signal over to client, with info confirming the gamemode.
    - Client switches into a loading screen
    - Paths and scenes to prepare:
    - Load map
    - Other players' information
    - Game Validation Signal
    - Game UI is loaded at this point, Server and Login UI are hidden.

- When clients recieve validation signal GameState transitions into validation.
    - Start Sending back to the server everything that they're ready and loaded, 
        - They send back that they have the full player list, and what parameters to load the game with
    - GameNetEngine Starts, and loads user input configurations,
        - Loads input libraries and establishes heartbeat with server
        - Starts up command frames and rollback data.
    - GameNetEngine Spawns ActorPool
- Server Sends Game Start to all clients
- Clients go into game start mode
    - When game start signal is recieved, the clients will begin gameplay.


# Spawner ref flow

/root/
    - Dungeon<GameLevel>
        - Entities



# Movement and reconciliation flow

On Local Client:

1. Get the local input, and begin accumulating movement.
2. While accumulating movement,
    - Resolve our current Location and DeltaPosition off of the last command frame.
    - Present location and rotation

