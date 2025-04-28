#!/bin/sh

DIR=generated

mkdir -p $DIR

mono bin/igorc-elixir.exe -d -v -t elixir \
    -x 'gen_elixir/*.cs' \
    -p igor/common \
    -p igor/db \
    -p igor/chronos \
    -p igor/bamboo \
    -p igor/visma \
    -p igor/junipeer \
    -p igor/web \
    -o $DIR \
  *.igor

exit 0

mono bin/igorc-elixir.exe -d -v -t elixir \
    -x 'gen_elixir/*.cs' \
    -i 'igor/common/*.igor' \
    -i 'igor/db/*.igor' \
    -i 'igor/visma/*.igor' \
    -i 'igor/junipeer/*.igor' \
    -o $DIR \
  igor/web/*.igor
