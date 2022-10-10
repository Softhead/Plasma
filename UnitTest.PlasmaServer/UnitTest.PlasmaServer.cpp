#include "pch.h"
#include "CppUnitTest.h"
#include "PlasmaServer.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace Plasma::Server;

TEST_CLASS(UnitTestPlasmaServer)
{
public:
		
	TEST_METHOD(GetErrorText_Unrecognized)
	{
		Assert::AreEqual(std::string("Unrecognized error number: -1"), GetErrorText(-1));
	}
};
