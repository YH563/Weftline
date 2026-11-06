#pragma once

#include <memory>

namespace weftline::support {

    /**
     * @brief 递归节点使用的独占指针。
     *
     * 目前是 std::unique_ptr；后续如果引入 arena，可以只替换这个别名。
     */
    template <typename T>
    using Box = std::unique_ptr<T>;

} // namespace weftline::support
