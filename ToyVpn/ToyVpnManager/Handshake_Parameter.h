#pragma once
#include <minwindef.h>
struct HANDSHAKE_PARAMETER
{
	int socketType;
	char* remoteHostNamePtr;
	char* remoteServiceNamePtr;
	BYTE* bytesToWritePtr;
	int bytesToWriteLength;
};