if [ ! -d "./yak/" ]
then
    mkdir "./yak/"
fi

dd if="./glTF-BinExporter/bin/Release/net48/Newtonsoft.Json.dll" of="./yak/Newtonsoft.Json.dll"
dd if="./glTF-BinExporter/bin/Release/net48/glTFLoader.dll" of="./yak/glTFLoader.dll"
dd if="./glTF-BinExporter/bin/Release/net48/glTFExtensions.dll" of="./yak/glTFExtensions.dll"

dd if="./glTF-BinExporter/bin/Release/net48/glTF-BinExporter.rhp" of="./yak/glTF-BinExporter.rhp"
dd if="./glTF-BinImporter/bin/Release/glTF-BinImporter.rhp" of="./yak/glTF-BinImporter.rhp"