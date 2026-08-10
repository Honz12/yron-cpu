; Shared definitions and a reusable delay routine
%define DELAY_STEPS 50

delay:
    LDIb $03, DELAY_STEPS
    LDIb $0A, 1
.loop:
    SUB $03, $0A, $03
    JNZ .loop, $03
    RET
