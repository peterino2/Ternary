@SET DEPLOYHOST=
FOR /F %%I IN ('cat deployhost.txt') DO @SET "DEPLOYHOST=%%I"

rm -f spriteTest.zip
tar -vv -a -c -f "Build/Server/spriteTest.zip" "Build/Server/spriteTest.x86_64" "Build/Server/data_Sprite Test_x86_64" "Build/Server/spriteTest.sh" "Build/Server/*.so" "Build/Server/*.pck"
scp "Build/Server/spriteTest.zip" %DEPLOYHOST%:~/dungeon/
ssh -t %DEPLOYHOST% "cd ~/dungeon && unzip spriteTest.zip && chmod +x Build/Server/spriteTest.x86_64"
