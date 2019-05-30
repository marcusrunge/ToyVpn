#pragma once
#include "Capsule.h"
#include "Handshake_Parameter.h"

#ifdef  __cplusplus
extern "C" {
#endif	
	typedef int(__cdecl* CallbackTemplate)(int number, char* option);
	__declspec(dllexport) char* ExternHandleHandshakeResponse(char* response);
	__declspec(dllexport) void ExternInitializeCallbackTemplate(CallbackTemplate callbackTemplate);
	__declspec(dllexport) struct HANDSHAKE_PARAMETER* ExternInitializeHandshake(char* vpnConfig);
	__declspec(dllexport) struct CAPSULE* ExternEncapsulate(struct CAPSULE* capsuleToEncapsulate);
	__declspec(dllexport) struct CAPSULE* ExternDecapsulate(struct CAPSULE* capsuleToDecapsulate);
# ifdef  __cplusplus
}
# endif