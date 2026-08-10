; Assembler feature demo: .define, .include, expressions, strings, chars, .align
.define GREETING "Hello; world"
.define NL 0x0A
.define MSG_ADDR 0x100

    .org 0
start:
    LDIb $04, 'A'          ; char literal -> 0x41
    LDIb $06, (2 + 3) * 4  ; parenthesized expression -> 20
    LDIb $07, 0b1010       ; binary -> 10
    LDIw $08, 1 << 8       ; shift -> 256
    LDIw $05, MSG_ADDR     ; macro + label expression
    CALL delay
    .byte 0xFF             ; halt

.include "common.inc"
.include "common.inc"      ; already included -> skipped

    .org 0x100
msg:
    .asciz GREETING
    .byte NL
    .ascii "a;b"
    .byte 0x01
    .align 4
