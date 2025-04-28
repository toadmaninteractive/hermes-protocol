#!/bin/sh

mono bin/igorc.exe -v -t ts \
    -x 'gen_ts/*.cs' \
    -p igor/common \
    -p igor/db \
    -p igor/web \
    -o ../src/app/protocol \
    *.igor

cp ts/igor.ts ../src/app/protocol
