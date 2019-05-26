#pragma once
#include "Capsule.h"
#include "Handshake_Parameter.h"

#ifdef  __cplusplus
extern "C" {
#endif	
	typedef int(__stdcall* CallbackTemplate)(int number, char* option);
	__declspec(dllexport) char* ExternHandleHandshakeResponse(char* response);
	__declspec(dllexport) void ExternInitializeCallbackTemplate(CallbackTemplate callbackTemplate);
	__declspec(dllexport) struct HANDSHAKE_PARAMETER* ExternInitializeHandshake(char* vpnConfig);
	__declspec(dllexport) CAPSULE* ExternEncapsulate(CAPSULE* capsuleToEncapsulate);
	__declspec(dllexport) CAPSULE* ExternDecapsulate(CAPSULE* capsuleToDecapsulate);
# ifdef  __cplusplus
}
# endif