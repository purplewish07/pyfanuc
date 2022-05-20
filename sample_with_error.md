# request
<pre>
a0 a0 a0 a0 00 01 21 01
	00 fe [length]
		00 09 [9 subpackets]
			00 1c [subpacket 1 length]
				00 01 00 01 00 1a
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 [ALARM]
			00 1c [subpacket 2 length]
				00 01 00 01 00 1c
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 [ACT RUN/MAIN]
			00 1c [subpacket 3 length]
				00 01 00 01 00 1d
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 [ACT SEQ]
			00 1c [subpacket 4 length]
				00 01 00 01 00 24
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 [ACTF]
			00 1c [subpacket 5 length]
				00 01 00 01 00 25
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 [ACTS]
			00 1c [subpacket 6 length]
				00 01 00 01 00 26
				00 00 00 04 ff ff ff ff 00 00 00 00 00 00 00 00 00 00 00 00 [POS ABS]
			00 1c [subpacket 7 length]
				00 01 00 01 00 26
				00 00 00 01 ff ff ff ff 00 00 00 00 00 00 00 00 00 00 00 00 [POS REF(MACHINE)]
			00 1c [subpacket 8 length]
				00 01 00 01 00 26
				00 00 00 06 ff ff ff ff 00 00 00 00 00 00 00 00 00 00 00 00 [POS REL]
			00 1c [subpacket 9 length]
				00 01 00 01 00 26
				00 00 00 07 ff ff ff ff 00 00 00 00 00 00 00 00 00 00 00 00 [POS REMAIN DIST]
</pre>
# response
<pre>
a0 a0 a0 a0 00 02 21 02
	01 6a [length]
		00 09 [9 subpackets]
			00 14 [subpacket 1 length]
				00 01 00 01 00 1a 00 00 00 00 00 00 [ok - no error]
					00 04 [payload length]
						00 00 00 00 [no alarm]
			00 18 [subpacket 2 length]
				00 01 00 01 00 1c 00 00 00 00 00 00 [ok - no error]
					00 08 [payload length]
						00 00 0b b8 00 00 0b b8 [running and main O3000]
			00 14 [subpacket 3 length]
				00 01 00 01 00 1d 00 00 00 00 00 00 [ok - no error]
					00 04 [payload length]
						00 00 00 00	[active seq 0]
			00 18 [subpacket 4 length]
				00 01 00 01 00 24 00 00 00 00 00 00
					00 08 [payload length]
						00 00 00 00 00 0a 00 04 [feedrate 0]
			00 10 [subpacket 5 length]
				00 01 00 01 00 25 00 01 00 00 00 00 00 00 [error #1 function not implemented]
			00 50 [subpacket 6 length]
				00 01 00 01 00 26 00 00 00 00 00 00 [ok - no error]
					00 40 [payload length]
						00 2f 7a e9 00 0a 00 04 00 02 bc 02 00 0a 00 04
						00 00 00 00 00 0a 00 04 00 00 00 00 00 0a 00 04
						ff ec ed 30 00 0a 00 04 00 36 ee 80 00 0a 00 04
						00 00 00 00 00 0a 00 04 00 00 00 00 00 0a 00 04
			00 50 [subpacket 7 length]
				00 01 00 01 00 26 00 00 00 00 00 00 [ok - no error]
					00 40 [payload length]
						ff fd b0 bd 00 0a 00 04 ff ed 51 11 00 0a 00 04
						ff ff f7 ad 00 0a 00 04 ff ff ff 9d 00 0a 00 04
						ff ec ed 30 00 0a 00 04 00 32 2a 47 00 0a 00 04
						00 00 00 00 00 0a 00 04 00 00 00 00 00 0a 00 04
			00 50 [subpacket 8 length]
				00 01 00 01 00 26 00 00 00 00 00 00 [ok - no error]
					00 40 [payload length]
						00 16 d1 05 00 0a 00 04 00 04 78 0b 00 0a 00 04
						ff ff fe 62 00 0a 00 04 00 00 10 af 00 0a 00 04
						00 0a e5 67 00 0a 00 04 e4 18 b6 0e 00 0a 00 04
						00 00 00 00 00 0a 00 04 00 00 00 00 00 0a 00 04
			00 10 [subpacket 9 length]
				00 01 00 01 00 26 00 06 00 00 00 00 00 00 [error #6 option error]
</pre>
