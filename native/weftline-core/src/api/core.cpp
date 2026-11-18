#include "weftline/core.h"

#include <cstdlib>

// C ABI 边界：所有 C++ 异常都必须在这里拦截。
extern "C" {

    const char* weft_version(void)
    {
        // 静态字符串，调用方只读，不负责释放。
        return "0.2.0";
    }

    void weft_free(char* pointer)
    {
        std::free(pointer);
    }

} // extern "C"
