All instructions are little-endian

Argument tags:

- `RB`: Raw Byte - A raw 8-bit value embedded in the code.
- `RW`: Raw Word - A raw 16-bit value embedded in the code.
- `RD`: Raw Double word - A raw 32-bit value embedded in the code.
- `reg`: Register reference.

ALIAS|ARG 1         |ARG 2          |ARG 3          | Description
-----|--------------|---------------|---------------|------------------------
NOP  |-             |-              |-              |Does nothing
INT  |Reason `RB`   |-              |-              |Pushes the return address to the stack and calls an interrupt
CALL |Address `RD`  |-              |-              |Pushes the return address to the stack and jumps to an address
RET  |-             |-              |-              |Pops a PC value from the stack and jumps to it
LDIb |Register `reg`|Value `RB`     |-              |Loads a constant 8-bit value stored in the instruction
LDIw |Register `reg`|Value `RW`     |-              |Loads a constant 16-bit stored in the instruction
LDId |Register `reg`|Value `RD`     |-              |Loads a constant 32-bit stored in the instruction
LDb  |Register `reg`|Address `reg`  |-              |Loads an 8-bit value from RAM specified by the address
LDw  |Register `reg`|Address `reg`  |-              |Loads a 16-bit value from RAM specified by the address
LDd  |Register `reg`|Address `reg`  |-              |Loads a 32-bit value from RAM specified by the address
STb  |Register `reg`|Address `reg`  |-              |Stores an 8-bit value into RAM specified by the address
STw  |Register `reg`|Address `reg`  |-              |Stores a 16-bit value into RAM specified by the address
STd  |Register `reg`|Address `reg`  |-              |Stores a 32-bit value into RAM specified by the address
MOV  |Dest `reg`    |Source `reg`   |-              |Copies over a value
ADD  |A `reg`       |B `reg`        |Dest `reg`     |Arithmetic operation
SUB  |A `reg`       |B `reg`        |Dest `reg`     |Arithmetic operation
MUL  |A `reg`       |B `reg`        |Dest `reg`     |Arithmetic operation
DIV  |A `reg`       |B `reg`        |Dest `reg`     |Arithmetic operation
MOD  |A `reg`       |B `reg`        |Dest `reg`     |Arithmetic operation
AND  |A `reg`       |B `reg`        |Dest `reg`     |Bitwise operation
NAND |A `reg`       |B `reg`        |Dest `reg`     |Bitwise operation
OR   |A `reg`       |B `reg`        |Dest `reg`     |Bitwise operation
NOR  |A `reg`       |B `reg`        |Dest `reg`     |Bitwise operation
XOR  |A `reg`       |B `reg`        |Dest `reg`     |Bitwise operation
EQ   |A `reg`       |B `reg`        |Dest `reg`     |Compare operation
GT   |A `reg`       |B `reg`        |Dest `reg`     |Compare operation
GTE  |A `reg`       |B `reg`        |Dest `reg`     |Compare operation
LT   |A `reg`       |B `reg`        |Dest `reg`     |Compare operation
LTE  |A `reg`       |B `reg`        |Dest `reg`     |Compare operation
JMP  |Address `RD`  |-              |-              |Jumps to a value
JNZ  |Address `RD`  |Cond `reg`     |-              |Jumps to a value if `Cond != 0`
JZ   |Address `RD`  |Cond `reg`     |-              |Jumps to a value if `Cond == 0`
PUSH |Value `reg`   |Size `RB`      |-              |Pushes a value to the stack, Size correlates to the number of bytes pushed: 0 -> 1 Byte, 1 -> 2 Bytes, 2 -> 4 Bytes
POP  |Dest `reg`    |Size `RB`      |-              |Pops a value from the stack, Size correlates to the number of bytes popped: 0 -> 1 Byte, 1 -> 2 Bytes, 2 -> 4 Bytes