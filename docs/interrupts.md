An interrupt stops the current program execution, jumps to an address and is expected to return.

Interrupts can be called via the `INT <reason>` instruction, or by an external device.

The call address of interrupts is stored in the interrupt table.

The index of the interrupt address entry is called a "reason".

## Version 1

The interrupt table consists of 128 entries located at the address 0x200, each having a size of 4 bytes.

MEMORY MAP

```
 CODE 512 BYTES
---------------------------
 INTERRUPT TABLE 512 BYTES
---------------------------
 CODE ...
```

Technically the table can be extended to 1024 bytes to support `0x80` - `0xFF` interrupt reasons

Predefined interrupt reasons

Hex |Description
----|---------------------
`00`|Error description
`01`|Device initialization
`02`|Input interrupt

### Error description

(Not defined by standard)

### Device initialization

Register|Storing                    |Initial value
--------|---------------------------|-------------
`$03`   |Device ID                  |Device-specified
`$04`   |Initialization finished    |`0`
`$05`   |Memory needed              |Device-specified
`$06`   |Memory address             |Software-specified

When `$04` is set to one, the device initialization is done.

### Input interrupt

Register|Storing                    |Initial value
--------|---------------------------|-------------
`$03`   |Device ID                  |Device-specified
`$04`   |Input value                |Device-specified
