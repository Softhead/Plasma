#pragma once
#include "..\PlasmaLibrary\DatabaseDefinition.h"
#include <string>
using namespace std;

namespace Plasma::Server
{
	class PlasmaServer
	{
	private:
		Database::DatabaseDefinition* definition_;
		const int SetDefinition(string line);

	public:
		const int CreateNew(Database::DatabaseDefinition* definition, string definitionFileName);
		const int Start(string definitionFileName);
	};

	string GetErrorText(const int errorNumber);
}
