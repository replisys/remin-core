#include <windows.h>
#include <stdint.h>
#include <wrl.h>
#include <string>

#include <stdio.h>

#define _DEBUG
#include <assert.h>

using ulong = ULONG;

class __declspec(uuid("1c8adb85-982e-47f9-999f-b0c3bf9d0449")) ICSIExternalTransformerExecutor : public IUnknown
{
public:
	virtual HRESULT WINAPI Initialize(DWORD flags, DWORD64 referenceVersion, LPCWSTR referenceImage, LPCWSTR fileStorage, LPCWSTR regStorage) = 0;
	
	virtual HRESULT WINAPI Install(LPCWSTR manifest, LPCWSTR transformId) = 0;
	
	virtual HRESULT WINAPI Uninstall(LPCWSTR a1, LPCWSTR a2) = 0;
	
	virtual HRESULT WINAPI Commit(LPCWSTR userSid, LPCWSTR loadedUserHive, LPCWSTR loadedUserClassesHive, LPCWSTR userProfilePath) = 0;
};

class __declspec(uuid("587bf538-4d90-4a3c-9ef1-58a200a8a9e7")) IDefinitionIdentity : public IUnknown
{

};

class ICSITransaction;

struct _CSI_FILE
{
	int unk;
	IDefinitionIdentity* id[2];
	LPCWSTR name;
};

class __declspec(uuid("465f1ec1-7f1d-4a85-a30b-ae1090f212db")) ICSIStore : public IUnknown
{
public:
	virtual HRESULT WINAPI BeginTransaction(ulong,_GUID const &,wchar_t const *,ICSITransaction * *) = 0;
	virtual HRESULT WINAPI CancelPendingTransaction(ulong,_GUID const &,wchar_t const *,ulong *) = 0;
	virtual HRESULT WINAPI BeginRepairTransaction(ulong,void * *,ulong *) = 0;
	virtual HRESULT WINAPI CancelPendingRepairTransaction(ulong,ulong *) = 0;
	virtual HRESULT WINAPI GetComponentManifests(ulong,unsigned __int64,void * *,_GUID const &,IUnknown * *) = 0;
	virtual HRESULT WINAPI GetComponentInstalledVersions(ulong,unsigned __int64,void * *,ulong * const,void * const) = 0;
	virtual HRESULT WINAPI GetComponentInformation(ulong,ulong,void *,unsigned __int64,void *) = 0;
	virtual HRESULT WINAPI ReplaceMacros(ulong,void *,wchar_t const *,wchar_t * *) = 0;
	virtual HRESULT WINAPI EnumPendingTransactions(ulong,_GUID const &,IUnknown * *) = 0;
	virtual HRESULT WINAPI CancelPendingTransactions(ulong,unsigned __int64,wchar_t const * const *,ulong *) = 0;
};

class IEnumCSI_FILE;

class __declspec(uuid("16b07adc-182f-4fe3-bc9b-e53991770f25")) ICSITransaction : public IUnknown
{
public:
 virtual HRESULT WINAPI InstallDeployment(ulong,IDefinitionIdentity *,wchar_t const *,wchar_t const *,wchar_t const *,wchar_t const *,wchar_t const *,wchar_t const *,ulong *) = 0;
 virtual HRESULT WINAPI PinDeployment(ulong,IDefinitionIdentity *,wchar_t const *,wchar_t const *,wchar_t const *,wchar_t const *,wchar_t const *,wchar_t const *,unsigned __int64,ulong *) = 0;
 virtual HRESULT WINAPI UninstallDeployment(ulong,IDefinitionIdentity *,wchar_t const *,wchar_t const *,wchar_t const *,ulong *) = 0;
 virtual HRESULT WINAPI UnpinDeployment(ulong,IDefinitionIdentity *,wchar_t const *,wchar_t const *,wchar_t const *,ulong *) = 0;
 virtual HRESULT WINAPI EnumMissingComponents(ulong,void * *,ulong *) = 0;
 virtual HRESULT WINAPI AddComponent(ulong,IDefinitionIdentity *,wchar_t const *,ulong *) = 0;
 virtual HRESULT WINAPI EnumMissingFiles(ulong,IEnumCSI_FILE * *) = 0;
 virtual HRESULT WINAPI AddFile(ulong,IDefinitionIdentity *,wchar_t const *,wchar_t const *,DWORD *) = 0;
 virtual HRESULT WINAPI Analyze(ulong,_GUID const &,IUnknown * *) = 0;
 virtual HRESULT WINAPI Commit(ulong,void *,ulong *) = 0;
 virtual HRESULT WINAPI Abort(ulong,ulong *) = 0;
 virtual HRESULT WINAPI Scavenge(ulong,IDefinitionIdentity *,wchar_t const *,wchar_t const *,ulong *) = 0;
};

class __declspec(uuid("0e695bd1-628c-40a1-88cf-925083986d16")) ICSITransaction2 : public IUnknown
{
public:
 virtual HRESULT WINAPI AddFiles(ulong,unsigned __int64,IDefinitionIdentity * * const,wchar_t const * * const,wchar_t const * * const,ulong * const,ulong *,unsigned __int64 *) = 0;
 virtual HRESULT WINAPI AddComponents(ulong,unsigned __int64,IDefinitionIdentity * * const,wchar_t const * * const,ulong *,unsigned __int64 *) = 0;
 virtual HRESULT WINAPI Scavenge(ulong,void *,IDefinitionIdentity *,wchar_t const *,wchar_t const *,ulong *) = 0;
 virtual HRESULT WINAPI Analyze(ulong,_GUID const &,IUnknown * *,ulong *) = 0;
 virtual HRESULT WINAPI UnstageDeploymentPayload(ulong,IDefinitionIdentity *,wchar_t const *,wchar_t const *,wchar_t const *,ulong *) = 0;
 virtual HRESULT WINAPI MarkDeploymentStaged(ulong,IDefinitionIdentity *,wchar_t const *,wchar_t const *,wchar_t const *,ulong *) = 0;
 virtual HRESULT WINAPI MarkDeploymentUnstaged(ulong,IDefinitionIdentity *,wchar_t const *,wchar_t const *,wchar_t const *,ulong *) = 0;
};

class IEnumCSI_FILE : public IUnknown
{
	public:
		virtual HRESULT WINAPI Next(int flags, _CSI_FILE* file, BOOL* fetched) = 0;
};

using namespace Microsoft::WRL;

struct CREATE_WINDOWS_PARAMS
{
	DWORD cbSize;
	char pad[12];
	LPCWSTR systemDir;
	char pad2[72];
	int arch; // 9 is amd64, best arch
	char pad3[128];
};

struct OFFLINE_STORE_CREATION_PARAMETERS
{
	DWORD cbSize; // 70
	LPCWSTR pszHostSystemDrivePath;
	LPCWSTR pszHostWindowsDirectoryPath;
	//LPCWSTR pszHostWindowsDirectoryPath;
};

using tWcpInitialize = HRESULT(WINAPI*)(HANDLE* wcpHandle);
using tWcpGetSystemStore = HRESULT(WINAPI*)(DWORD dwFlags, const GUID& iid, void** ppIface);
using tWcpSetIsolationIMalloc = HRESULT(WINAPI*)(IMalloc*);
using tCreateNewWindows = HRESULT(WINAPI*)(DWORD dwFlags, const wchar_t* systemDrive, CREATE_WINDOWS_PARAMS* params, void** registryKeys, void* unk);
using tWcpGetOpenExistingOfflineStore = HRESULT(WINAPI*)(DWORD dwFlags, CREATE_WINDOWS_PARAMS* pParameters, const GUID& iid, void** ppi, DWORD* meh);
using tParseManifest = HRESULT(WINAPI*)(const wchar_t* mp, void* cb, const GUID& riid, void** man);
using tWcpDismountRegistryHives = HRESULT(WINAPI*)(void*);

int wmain(int argc, wchar_t* argv[])
{
	using namespace std::string_literals;

	CoInitialize(NULL);

    if (argc < 3)
    {
        return 1;
    }

    const wchar_t* manifestRoot = argv[1];
    const wchar_t* vhdRoot = argv[2];

	HMODULE hWcp = LoadLibraryW((manifestRoot + L"\\wcp.dll"s).c_str());
	assert(hWcp);
	
	printf("ok1\n");
	
	auto WcpInitialize = (tWcpInitialize)GetProcAddress(hWcp, "WcpInitialize");	
	auto WcpGetSystemStore = (tWcpGetSystemStore)GetProcAddress(hWcp, "GetSystemStore");
	auto WcpOpenExistingOfflineStore = (tWcpGetOpenExistingOfflineStore)GetProcAddress(hWcp, "OpenExistingOfflineStore");
	auto WcpSetIsolationIMalloc = (tWcpSetIsolationIMalloc)GetProcAddress(hWcp, "SetIsolationIMalloc");
	auto CreateNewWindows = (tCreateNewWindows)GetProcAddress(hWcp, "CreateNewWindows");
	auto ParseManifest = (tParseManifest)GetProcAddress(hWcp, "ParseManifest");
	auto WcpDismountRegistryHives = (tWcpDismountRegistryHives)GetProcAddress(hWcp, "DismountRegistryHives");

	assert(WcpInitialize);
	assert(WcpGetSystemStore);

	printf("ok2\n");
	
	HANDLE wcp;
	assert(SUCCEEDED(WcpInitialize(&wcp)));
	
	IMalloc* malloc;
	CoGetMalloc(1, &malloc);
	
	WcpSetIsolationIMalloc(malloc);
	
	printf("ok3\n");
	
	ComPtr<ICSIExternalTransformerExecutor> executor;
	
	CREATE_WINDOWS_PARAMS params = {0};
	params.cbSize = 0x70;
	params.systemDir = vhdRoot;
	params.arch = 9;
	
	void* rks; // NOTE: call DismountRegistryHives on these!
	assert(SUCCEEDED(CreateNewWindows(0, L"C:\\", &params, &rks, NULL)));
	
	ComPtr<ICSIStore> store;
	assert(SUCCEEDED(WcpOpenExistingOfflineStore(0, &params, __uuidof(ICSIStore), (void**)&store, NULL)));
	
	ComPtr<ICSITransaction> txn;
	assert(SUCCEEDED(store->BeginTransaction(0, __uuidof(ICSITransaction), L"servicingStuff", &txn)));
	
	auto cs = {
        L"Microsoft-Windows-Deployment-Image-Servicing-Management",
        L"Microsoft-Windows-Deployment-Image-Servicing-Management-API",
        L"Microsoft-Windows-Deployment-Image-Servicing-Management-Core",
		L"Microsoft-Windows-Deployment-Image-Servicing-Management-WinProviders",
        L"Microsoft-Windows-PackageManager",
        L"Microsoft-Windows-PantherEngine",
		L"Microsoft-Windows-ServicingStack" };
	
	for (auto& cr : cs)
	{
        wchar_t c[MAX_PATH];
        swprintf_s(c, L"%s\\%s.manifest", manifestRoot, cr);

		ComPtr<IDefinitionIdentity> di;
		assert(SUCCEEDED(ParseManifest(c, NULL, __uuidof(IDefinitionIdentity), (void**)&di)));
		
		assert(SUCCEEDED(txn->AddComponent(0, di.Get(), c, NULL)));
	}
	
	ComPtr<IEnumCSI_FILE> en;
	assert(SUCCEEDED(txn->EnumMissingFiles(5, &en)));
	
	BOOL fetched = 0;
	_CSI_FILE f;
	assert(SUCCEEDED(en->Next(1, &f, &fetched))); 
	
	while (fetched)
	{
		DWORD disp = 0;
		assert(SUCCEEDED(txn->AddFile(0, f.id[0], f.name, (std::wstring(manifestRoot) + L"\\" + f.name).c_str(), &disp)));
		assert(SUCCEEDED(en->Next(1, &f, &fetched))); 
	}
	
	auto deployments = { L"remin-deployment", L"Microsoft-Windows-CoreSystem-DISM-Deployment" };

	for (auto& dr : deployments)
	{
        wchar_t d[MAX_PATH];
        swprintf_s(d, L"%s\\%s.manifest", manifestRoot, dr);

		ComPtr<IDefinitionIdentity> di2;
		assert(SUCCEEDED(ParseManifest(d, NULL, __uuidof(IDefinitionIdentity), (void**)&di2)));
		
		assert(SUCCEEDED(txn->PinDeployment(0, di2.Get(), NULL, NULL, NULL, NULL, d, NULL /* no cat? */, 0, NULL)));
		
		ComPtr<ICSITransaction2> txn2;
		txn.As(&txn2);
		
		assert(SUCCEEDED(txn2->MarkDeploymentStaged(0, di2.Get(), NULL, NULL, NULL, NULL)));
		
		assert(SUCCEEDED(txn->InstallDeployment(0, di2.Get(), NULL, NULL, NULL, NULL, d, NULL /* no cat? */, NULL)));
	}
	
	
	if (!SUCCEEDED(txn->Commit(0, NULL, NULL)))
    {
        printf("error!\n");
    }
	
	WcpDismountRegistryHives(rks);
	
	return 0;
}