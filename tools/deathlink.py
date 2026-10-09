#!/usr/bin/env python3
"""
A DeathLink stand-in for the test room: plays "the other game" of the multiworld.

    python tools/deathlink.py listen              # prints every DeathLink the room passes on
    python tools/deathlink.py send                # sends one, as if another game had died
    python tools/deathlink.py send --cause "fell off a bike"
    python tools/deathlink.py admin "/send BWTraps Big Drop"   # a server command (testroom.py's room)
    python tools/deathlink.py traplink --cause "Ice Trap"       # a TrapLink, as another game's trap

It joins as a text-only client on a slot of the room (`--slot`, BWWatch by default:
tools/players/traps-0-4-0.yaml has it) with the DeathLink tag, so it hears what Big Walk
sends and Big Walk hears what it sends. Needs the `websockets` package (Archipelago's own).
"""

from __future__ import annotations

import argparse
import asyncio
import json
import time
import uuid

import websockets


async def connect(url: str, slot: str):
    socket = await websockets.connect(url, max_size=None)
    await socket.recv()  # RoomInfo
    await socket.send(json.dumps([{
        "cmd": "Connect",
        "game": "",
        "name": slot,
        "password": "",
        "uuid": str(uuid.uuid4()),
        "version": {"major": 0, "minor": 6, "build": 0, "class": "Version"},
        "items_handling": 0,
        "tags": ["TextOnly", "DeathLink"],
        "slot_data": False,
    }]))
    for packet in json.loads(await socket.recv()):
        if packet.get("cmd") == "ConnectionRefused":
            raise SystemExit(f"Refused: {packet.get('errors')}")
    return socket


async def listen(url: str, slot: str) -> None:
    socket = await connect(url, slot)
    print(f"Listening as {slot}; Ctrl+C to stop.", flush=True)
    async for message in socket:
        for packet in json.loads(message):
            if packet.get("cmd") == "Bounced" and "DeathLink" in packet.get("tags", []):
                data = packet.get("data", {})
                print(f"{time.strftime('%H:%M:%S')} DeathLink from {data.get('source')}: {data.get('cause')}", flush=True)
            elif packet.get("cmd") == "Bounced" and "TrapLink" in packet.get("tags", []):
                data = packet.get("data", {})
                print(f"{time.strftime('%H:%M:%S')} TrapLink from {data.get('source')}: {data.get('trap_name')}", flush=True)


async def send(url: str, slot: str, cause: str) -> None:
    socket = await connect(url, slot)
    await socket.send(json.dumps([{
        "cmd": "Bounce",
        "tags": ["DeathLink"],
        "data": {"time": time.time(), "source": slot, "cause": cause},
    }]))
    print(f"DeathLink sent as {slot}: {cause}")
    await socket.close()


async def traplink(url: str, slot: str, trap: str) -> None:
    socket = await connect(url, slot)
    await socket.send(json.dumps([{
        "cmd": "Bounce",
        "tags": ["TrapLink"],
        "data": {"time": time.time(), "source": slot, "trap_name": trap},
    }]))
    print(f"TrapLink sent as {slot}: {trap}")
    await socket.close()


async def admin(url: str, slot: str, command: str, password: str) -> None:
    socket = await connect(url, slot)
    for line in (f"!admin login {password}", f"!admin {command}"):
        await socket.send(json.dumps([{"cmd": "Say", "text": line}]))
    deadline = time.time() + 3
    while time.time() < deadline:
        try:
            message = await asyncio.wait_for(socket.recv(), deadline - time.time())
        except asyncio.TimeoutError:
            break
        for packet in json.loads(message):
            if packet.get("cmd") == "PrintJSON":
                print("".join(part.get("text", "") for part in packet.get("data", [])))
    await socket.close()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("action", choices=["listen", "send", "admin", "traplink"])
    parser.add_argument("command", nargs="?", help="admin: the server command, e.g. \"/send BWTraps Big Drop\"")
    parser.add_argument("--password", default="bwtest", help="admin: the room's server password")
    parser.add_argument("--url", default="ws://localhost:38281")
    parser.add_argument("--slot", default="BWWatch")
    parser.add_argument("--cause", default="the test room says so")
    args = parser.parse_args()

    if args.action == "listen":
        asyncio.run(listen(args.url, args.slot))
    elif args.action == "traplink":
        asyncio.run(traplink(args.url, args.slot, args.cause))
    elif args.action == "admin":
        asyncio.run(admin(args.url, args.slot, args.command, args.password))
    else:
        asyncio.run(send(args.url, args.slot, args.cause))


if __name__ == "__main__":
    main()
