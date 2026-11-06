#include "weftline/core.h"

#include <cstdlib>
#include <cstring>
#include <exception>
#include <string>

#include "weftline/api/compile.hpp"

// C ABI 边界：所有 C++ 异常都必须在这里拦截。
extern "C" {

    const char* weft_version(void)
    {
        return "0.1.0";
    }

    char* weft_compile_json(const char* request_json)
    {
        const std::string response = [request_json]() -> std::string {
            try {
                if (request_json == nullptr) {
                    return weftline::api::error_json("request is null");
                }
                return weftline::api::compile_to_json(request_json);
            } catch (const std::exception& error) {
                return weftline::api::error_json(error.what());
            } catch (...) {
                return weftline::api::error_json("unknown native error");
            }
        }();

        // 用 malloc 分配，配合 weft_free，避免跨语言分配器不一致。
        auto* result = static_cast<char*>(std::malloc(response.size() + 1));
        if (result == nullptr) {
            return nullptr;
        }

        std::memcpy(result, response.c_str(), response.size() + 1);
        return result;
    }

    void weft_free(char* pointer)
    {
        std::free(pointer);
    }

} // extern "C"
