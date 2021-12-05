# Описание Walrus

и примером будет ЗЫЧ образ, в котором 3 типа данных, дата, xadpcm и cdda
первая проблема это архиватор, они все не очень
7z хорошо жмёт данные, не жмёт АУДИО вообще
RAR средненько жмёт данные и неплохо АУДИО
но аудио кодеры АУДИО жмут совсем замечательно

как морж работал раньше
морж сканил образ на одинаковые блоки и сохранял только отличающиеся
за счёт того, что в региональных версиях данные практически ничем не отличаются
выходит огромная экономия, за счёт удаления всех повторных чанок (или секторов)
это концепция

второй момент, в зыче, это отделение мух от котлет
тот же STR это интерлив, видео и аудио
морж отделяет данные от аудио по разным файлам
один тип данных от другого

т.е. раньше морж производил в случае с зычём, несколько файлов
файл с данными, файл с xadpcm и  файл с cdda 
файл с картой чанок и xml, в котором это соединялось воедино
всё, что не cdda жмётся 7z, cdda аудио кодеком
это неудобно, но максимально по сжатию

т.е. сначала надо распаковать данные, потом аудио, потом моржом всё обратно извлечь 
поэтому я родил следующую инкарнацию

## Формат MRG

в которой всё будет хранится сжато, жаться сразу и расжиматься
и всё будет в одном файле

- XML в сжатом виде в MRG хранится (XML жмётся встроенным в шарп zlib'ом, чтобы не быть привязанным к 7z, т.е. посмотреть внутренности MRG можно не имея 7z)
- в самом начал MRG, после мажикворда "MRG1"
- unt64 смещение в MRG, uint32 размер XML, и 16 байт следующие это MD5 запакованного XML
- поэтому партишны начинаются с 32 байта в MRG

## Пример XML

теперь смотрим в XML:
- два раздела, partitions и entries
- entries это метаданные и описание того, что в этих партишнах хранится
    - ссылка на карту чанок каждого файла
    - я ещё добавлю атрибуты и дату
    - И MD5 конечно
- а теперь партишны
    - 4 вида, form1, form2, audio, map
    - скорее всего ещё один тип будет

```xml
<root>
  <partitions>
    <partition type="form1" offset="32" u_length="67108864" c_length="28016632" />
    <partition type="form1" offset="28016664" u_length="67108864" c_length="21032884" />
    <partition type="form1" offset="49049548" u_length="2215936" c_length="538888" />
    <partition type="form2" offset="49588436" u_length="7757512" c_length="5902748" />
    <partition type="audio" offset="55491184" u_length="10584044" c_length="7663235" />
    <partition type="audio" offset="63154419" u_length="10584044" c_length="7488344" />
    <partition type="audio" offset="70642763" u_length="10584044" c_length="7208217" />
    <partition type="audio" offset="77850980" u_length="10584044" c_length="6693070" />
    <partition type="audio" offset="84544050" u_length="10584044" c_length="6875274" />
    <partition type="audio" offset="91419324" u_length="10584044" c_length="6982251" />
    <partition type="audio" offset="98401575" u_length="10584044" c_length="7198789" />
    <partition type="audio" offset="105600364" u_length="10584044" c_length="7030406" />
    <partition type="audio" offset="112630770" u_length="10584044" c_length="7287190" />
    <partition type="audio" offset="119917960" u_length="10584044" c_length="7113539" />
    <partition type="audio" offset="127031499" u_length="10584044" c_length="7329978" />
    <partition type="audio" offset="134361477" u_length="10584044" c_length="7378079" />
    <partition type="audio" offset="141739556" u_length="10584044" c_length="7087663" />
    <partition type="audio" offset="148827219" u_length="10584044" c_length="7274170" />
    <partition type="audio" offset="156101389" u_length="10584044" c_length="6829825" />
    <partition type="audio" offset="162931214" u_length="10584044" c_length="6786472" />
    <partition type="audio" offset="169717686" u_length="10584044" c_length="6669428" />
    <partition type="audio" offset="176387114" u_length="10584044" c_length="6650086" />
    <partition type="audio" offset="183037200" u_length="10584044" c_length="6757750" />
    <partition type="audio" offset="189794950" u_length="10584044" c_length="7060083" />
    <partition type="audio" offset="196855033" u_length="10584044" c_length="7023686" />
    <partition type="audio" offset="203878719" u_length="10584044" c_length="7231813" />
    <partition type="audio" offset="211110532" u_length="10584044" c_length="6694432" />
    <partition type="audio" offset="217804964" u_length="10584044" c_length="6706535" />
    <partition type="audio" offset="224511499" u_length="10584044" c_length="6781765" />
    <partition type="audio" offset="231293264" u_length="10584044" c_length="7048258" />
    <partition type="audio" offset="238341522" u_length="10584044" c_length="7011330" />
    <partition type="audio" offset="245352852" u_length="10584044" c_length="7307786" />
    <partition type="audio" offset="252660638" u_length="10584044" c_length="5182626" />
    <partition type="audio" offset="257843264" u_length="10584044" c_length="4387540" />
    <partition type="audio" offset="262230804" u_length="10584044" c_length="4381788" />
    <partition type="audio" offset="266612592" u_length="3497468" c_length="1448276" />
    <partition type="map" offset="268060868" c_length="104432" u_length="1896128" />
  </partitions>
  <entries>
    <entry type="file" mode="raw" name="X-Men - Children of the Atom (USA) (Track 01).bin" length="194239920" map_offset="0" map_length="1321360" />
    <entry type="file" mode="audio" name="X-Men - Children of the Atom (USA) (Track 02).bin" length="21511392" map_offset="1321360" map_length="36584" />
    <entry type="file" mode="audio" name="X-Men - Children of the Atom (USA) (Track 03).bin" length="12449136" map_offset="1357944" map_length="21172" />
    <entry type="file" mode="audio" name="X-Men - Children of the Atom (USA) (Track 04).bin" length="1164240" map_offset="1379116" map_length="1980" />
    <entry type="file" mode="audio" name="X-Men - Children of the Atom (USA) (Track 05).bin" length="1432368" map_offset="1381096" map_length="2436" />
    <entry type="file" mode="audio" name="X-Men - Children of the Atom (USA) (Track 06).bin" length="1171296" map_offset="1383532" map_length="1992" />
    <entry type="file" mode="audio" name="X-Men - Children of the Atom (USA) (Track 07).bin" length="20728176" map_offset="1385524" map_length="35252" />
    <entry type="file" mode="audio" name="X-Men - Children of the Atom (USA) (Track 08).bin" length="25342800" map_offset="1420776" map_length="43100" />
    <entry type="file" mode="audio" name="X-Men - Children of the Atom (USA) (Track 09).bin" length="23726976" map_offset="1463876" map_length="40352" />
    <entry type="file" mode="audio" name="X-Men - Children of the Atom (USA) (Track 10).bin" length="24209136" map_offset="1504228" map_length="41172" />
    <entry type="file" mode="audio" name="X-Men - Children of the Atom (USA) (Track 11).bin" length="20831664" map_offset="1545400" map_length="35428" />
    <entry type="file" mode="audio" name="X-Men - Children of the Atom (USA) (Track 12).bin" length="20119008" map_offset="1580828" map_length="34216" />
    <entry type="file" mode="audio" name="X-Men - Children of the Atom (USA) (Track 13).bin" length="25580352" map_offset="1615044" map_length="43504" />
    <entry type="file" mode="audio" name="X-Men - Children of the Atom (USA) (Track 14).bin" length="19999056" map_offset="1658548" map_length="34012" />
    <entry type="file" mode="audio" name="X-Men - Children of the Atom (USA) (Track 15).bin" length="21770112" map_offset="1692560" map_length="37024" />
    <entry type="file" mode="audio" name="X-Men - Children of the Atom (USA) (Track 16).bin" length="21464352" map_offset="1729584" map_length="36504" />
    <entry type="file" mode="audio" name="X-Men - Children of the Atom (USA) (Track 17).bin" length="22701504" map_offset="1766088" map_length="38608" />
    <entry type="file" mode="audio" name="X-Men - Children of the Atom (USA) (Track 18).bin" length="21593712" map_offset="1804696" map_length="36724" />
    <entry type="file" mode="audio" name="X-Men - Children of the Atom (USA) (Track 19).bin" length="32163600" map_offset="1841420" map_length="54700" />
    <entry type="file" mode="file" name="X-Men - Children of the Atom (USA).cue" length="2433" map_offset="1896120" map_length="8" />
  </entries>
</root>
```

## Метод сжатия

теперь, как сжатие идёт:
- морж сканирует образ, набирает 64 мега в буфер, отдаёт на паковку (отсюда u_length="67108864" unpacked length. этот параметр как раз можно будет менять, т.е. по сути, это размер словаря для 7z)
- партишны это вот эти 64 меговые буферы, хранятся друг за другом, на каждый ссылка: смещение в MRG и размер
- 64 мега у form1 секторов, чуть больше у form2 (размер сектора побольше), и у cdda одна минута
- хотя для cdda можно и поболее. form1 буферы сразу в MRG сохраняются, form2 и cdda в темп файлы
- потом, когда файлы заканчиваются, form2 и cdda приклеиваются к MRG
- приклеиваются карты чанок и XML
