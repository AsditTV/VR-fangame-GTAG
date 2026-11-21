#!/bin/bash
# Cris de screamer graves (un par monstre) : impact sub-basse + claquement sec + couche propre au monstre, saturés.
set -e
OUT="$(cd "$(dirname "$0")/../.." && pwd)/Assets/Horror/Audio/Jumpscare"
SR=48000
D=1.9
ENV='min(t/0.012,1)*if(lt(t,1.25),1,exp(-(t-1.25)*7))'
# impact commun : balayage 130 -> 32 Hz + claquement de bruit très court
SUB="sin(2*PI*(32*t+12.25*(1-exp(-8*t))))*exp(-2.2*t)*1.2"
SNAP="(random(0)*2-1)*exp(-45*t)*0.9"

make () {   # $1 nom  $2 expression de la couche propre  $3 filtres de la couche
  ffmpeg -v error -y \
    -f lavfi -i "aevalsrc='$SUB':s=$SR:d=$D" \
    -f lavfi -i "aevalsrc='$SNAP':s=$SR:d=$D" \
    -f lavfi -i "aevalsrc='$2':s=$SR:d=$D" \
    -filter_complex "[1]highpass=f=900[s];[2]$3[c];[0][s][c]amix=inputs=3:normalize=0,aeval='tanh(2.2*val(0))',lowshelf=g=6:f=70,equalizer=f=160:t=q:w=1.2:g=4,equalizer=f=2500:t=q:w=1:g=5,highpass=f=30,alimiter=limit=0.95,afade=t=out:st=1.6:d=0.3" \
    -ac 1 -ar $SR "$OUT/$1.wav"
  echo "OK $1"
}

# L'Écoute : rugissement rauque qui descend (dent de scie 240 -> 85 Hz + souffle filtré), tremblement à 14 Hz
make ecoute_cri \
  "(0.6*(2*(85*t+77.5*(1-exp(-2*t))-floor(85*t+77.5*(1-exp(-2*t))))-1)+0.7*(random(1)*2-1))*(0.75+0.25*sin(2*PI*14*t))*$ENV" \
  "bandpass=f=420:w=500,equalizer=f=180:t=q:w=1:g=8,volume=1.6"

# Le Doux : grognement étouffé dans la fourrure (dent de scie 62 Hz avec vibrato rapide), très grave et sourd
make doux_cri \
  "(0.9*(2*(62*t+0.6*sin(2*PI*23*t)-floor(62*t+0.6*sin(2*PI*23*t)))-1)+0.35*(random(2)*2-1))*(0.7+0.3*sin(2*PI*9*t))*$ENV" \
  "lowpass=f=650,equalizer=f=110:t=q:w=1:g=9,volume=1.1"

# Le Drap : cri mouillé, gargouillé (bruit balayé 900 -> 160 Hz, glouglou à 31 Hz), effet flanger
make drap_cri \
  "(random(3)*2-1)*(0.55+0.45*abs(sin(2*PI*31*t)))*$ENV + 0.5*sin(2*PI*(70*t+40*(1-exp(-3*t))))*$ENV" \
  "bandpass=f=500:w=700,flanger=delay=4:depth=6:speed=0.7,equalizer=f=160:t=q:w=1:g=7,volume=1.7"
