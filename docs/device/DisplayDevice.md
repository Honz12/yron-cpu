## `PROT:1.0`

The protocol version 1.0 uses 4 bytes to comunicate

Byte        | Use
------------|---------------------------------------------
`00`        | Command send (if not 0, command is executed)
`01`        | Command identifier
`02` & `03` | Arguments

### Known command identifiers

Identifier code | Byte `02`     | Byte `03`     | Description
----------------|---------------|---------------|-------------------
`00`            | -             | -             | Do nothing
`01`            | ASCII char    | -             | Prints a character to the screen
`02`            | -             | -             | Clears the screen
