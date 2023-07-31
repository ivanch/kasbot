# KasBot
#### Inspired by a Brazilian DJ

## Running
If using Docker, just put a token on the `docker-compose.yml` file and run it using `docker-compose up -d`.

If deploying it by hand, you can run:
```shell
dotnet build -c Release -o bin
dotnet bin/Kasbot.dll
```

## Commands
| Command | Description
| -- | -- |
| `!play <url/text>` | Plays a song from YouTube |
| `!skip` | Skips the current song |
| `!stop` | Stops the current song and clears the queue |
| `!leave` | Leaves the current voice channel and clears the queue |
| `!cat` | Sends a random cat pic into the channel :3 |

## Play flags
| Flag | Description
| -- | -- |
| `-s`, `-silent` | Don't send play message into channel |
| `-r`, `-repeat` | Repeat music |

You can change the command prefix by putting another symbol in `COMMAND_PREFIX` environment variable.