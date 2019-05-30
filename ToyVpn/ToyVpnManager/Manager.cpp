#include "pch.h"
#include "Manager.h"

CallbackTemplate _callbackTemplate = 0;

char* ExternHandleHandshakeResponse(char* response)
{
	return response;
}

void ExternInitializeCallbackTemplate(CallbackTemplate callbackTemplate)
{
	_callbackTemplate = callbackTemplate;
	_callbackTemplate(0, "option");
}

struct HANDSHAKE_PARAMETER* ExternInitializeHandshake(char* vpnConfig)
{
	char* remoteHostNamePtr = nullptr;
	char* remoteServiceNamePtr = nullptr;;
	BYTE* bytesToWritePtr = nullptr;;
	int bytesToWriteLength = 0;
	int socketType = -1;
	char* delimiter = "\r\n";
	char* remaining_lines = _strdup(vpnConfig);
	char* ptr = remaining_lines;
	char* current_line = strtok_s(remaining_lines, delimiter, &remaining_lines);
	remoteHostNamePtr = _strdup(current_line);
	int i = 0;
	while (current_line != NULL)
	{
		current_line = strtok_s(NULL, delimiter, &remaining_lines);
		if (current_line)
		{
			if (i == 0) remoteServiceNamePtr = _strdup(current_line);
			else if (i == 1)
			{
				char* secret = _strdup(current_line);
				bytesToWritePtr = (BYTE*)secret;
				bytesToWriteLength = strlen(secret);
			}
			else if (i == 2)
			{
				if (strncmp(current_line, "udp", 3) == 0) socketType = 0;
				else if (strncmp(current_line, "tcp", 3) == 0)socketType = 1;
			}
			//free(ptr);
		}
		i++;
	}
	struct HANDSHAKE_PARAMETER* handshakeParameter = (struct HANDSHAKE_PARAMETER*)malloc(sizeof(struct HANDSHAKE_PARAMETER));
	handshakeParameter->bytesToWritePtr = (BYTE*)calloc(bytesToWriteLength, sizeof(BYTE));
	handshakeParameter->remoteHostNamePtr = (char*)calloc(strlen(remoteHostNamePtr), sizeof(char));
	handshakeParameter->remoteServiceNamePtr = (char*)calloc(strlen(remoteServiceNamePtr), sizeof(char));
	handshakeParameter->bytesToWriteLength = bytesToWriteLength;
	handshakeParameter->bytesToWritePtr = bytesToWritePtr;
	handshakeParameter->remoteHostNamePtr = remoteHostNamePtr;
	handshakeParameter->remoteServiceNamePtr = remoteServiceNamePtr;
	handshakeParameter->socketType = socketType;
	return handshakeParameter;
}

struct CAPSULE* ExternEncapsulate(struct CAPSULE* capsuleToEncapsulate)
{
	return capsuleToEncapsulate;
}

struct CAPSULE* ExternDecapsulate(struct CAPSULE* capsuleToDecapsulate)
{
	return capsuleToDecapsulate;
}