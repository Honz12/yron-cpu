An interrupt stops the current program execution, calls an address and is expected to return.

Interrupts can be called via the `INT <reason>` instruction, or by a external device.

The call address of interrupts is stored in the interrupt table.

The index of the interrupt address entry is called a "reason".

## Version 1

The interrupt table consists 128 entries located the address 0x200, each having a size of 4 bytes.

MEMORY MAP

```
 CODE 512 BYTES
---------------------------
 INTERRUPT TABLE 512 BYTES
---------------------------
 CODE ...
```

Technicaly the table can be extended to 1024 bytes to support `0x80` - `0xFF` interrupt reasons
